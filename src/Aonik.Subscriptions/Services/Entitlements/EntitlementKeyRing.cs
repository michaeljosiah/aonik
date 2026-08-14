using System.Text;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Entitlements;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Subscriptions.Entities.Entitlements;
using Aonik.Subscriptions.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Subscriptions.Services.Entitlements;

/// <summary>
/// Signing keys and the published verification set (Spec 090 §6).
/// </summary>
internal sealed class EntitlementKeyRing : IEntitlementKeyRing
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly IEd25519Signer _signer;
    private readonly IEntitlementKeyProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly EntitlementTokenOptions _options;
    private readonly ILogger<EntitlementKeyRing> _logger;

    public EntitlementKeyRing(
        SubscriptionsDbContext dbContext,
        IEd25519Signer signer,
        IEntitlementKeyProtector protector,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<EntitlementTokenOptions> options,
        ILogger<EntitlementKeyRing> logger)
    {
        _dbContext = dbContext;
        _signer = signer;
        _protector = protector;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EntitlementKeyDescriptor> RotateAsync(
        TimeSpan signingLifetime,
        TimeSpan graceAllowance,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var (publicKey, privateKey) = _signer.GenerateKeyPair();

        var key = new EntitlementSigningKey
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Kid = BuildKid(now),
            Algorithm = "Ed25519",
            PublicKey = EntitlementTokenFormat.Base64UrlEncode(publicKey),
            ProtectedPrivateKey = _protector.Protect(Convert.ToBase64String(privateKey)),
            NotBefore = now,
            SigningNotAfter = now.Add(signingLifetime),
            // At least the maximum grace beyond the signing cutoff, and pushed later by issuance
            // itself under §6.1 — this is the floor, not the invariant.
            VerifyNotAfter = now.Add(signingLifetime).Add(graceAllowance),
            Status = EntitlementKeyStatuses.Active,
        };

        // The predecessor stops signing but STAYS PUBLISHED. Overlap is what makes rotation a
        // non-event; retirement is §6.1's separate, slower question.
        await _dbContext.EntitlementSigningKeys
            .Where(k => k.TenantId == tenantId && k.Status == EntitlementKeyStatuses.Active)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(k => k.Status, EntitlementKeyStatuses.Retiring)
                    .SetProperty(k => k.SigningNotAfter, now),
                cancellationToken);

        _dbContext.EntitlementSigningKeys.Add(key);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Entitlement signing key {Kid} generated; predecessor keys retiring but still published.",
            key.Kid);

        return Describe(key);
    }

    public async Task<EntitlementKeyDescriptor?> GetSigningKeyAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var key = await _dbContext.EntitlementSigningKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId
                && k.Status == EntitlementKeyStatuses.Active
                && k.NotBefore <= now
                && k.SigningNotAfter > now)
            .OrderByDescending(k => k.NotBefore)
            .FirstOrDefaultAsync(cancellationToken);

        return key is null ? null : Describe(key);
    }

    public async Task<PublishedKeySet> GetPublishedSetAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        // Everything still inside VerifyNotAfter and not withdrawn. A withdrawn key is ABSENT, and
        // absence is the message: the list is complete, never a delta, which is what makes withdrawal
        // enforceable on a client that bundled the key.
        var keys = await _dbContext.EntitlementSigningKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId
                && k.Status != EntitlementKeyStatuses.Withdrawn
                && k.VerifyNotAfter > now)
            .OrderBy(k => k.NotBefore)
            .Select(k => new { k.Kid, k.PublicKey })
            .ToListAsync(cancellationToken);

        var version = await ResolveSetVersionAsync(tenantId, cancellationToken);

        // The signed bytes are BUILT ONCE and carried verbatim, so no client re-serialises to verify —
        // the same trick as the token. Key order is fixed by the query, but that is a convenience for
        // diffing, not a contract: the contract is these exact bytes.
        var builder = new StringBuilder();
        builder.Append("{\"version\":").Append(version).Append(",\"keys\":[");

        for (var i = 0; i < keys.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(',');
            }

            builder
                .Append("{\"kid\":\"").Append(keys[i].Kid)
                .Append("\",\"alg\":\"Ed25519\",\"key\":\"").Append(keys[i].PublicKey)
                .Append("\"}");
        }

        builder.Append("]}");

        var signedBytes = Encoding.UTF8.GetBytes(builder.ToString());
        var signature = await SignWithRootAsync(tenantId, signedBytes, cancellationToken);

        return new PublishedKeySet(
            version,
            EntitlementTokenFormat.Base64UrlEncode(signedBytes),
            EntitlementTokenFormat.Base64UrlEncode(signature));
    }

    /// <summary>
    /// Monotonic set version: the count of key rows ever created plus withdrawals.
    ///
    /// <para>
    /// Monotonicity is the property the client depends on — a fetched set with a lower version is ignored, so an
    /// attacker cannot roll a client back to a set that still contains a compromised key. Deriving it from row
    /// history rather than storing a counter means there is no counter to fail to increment.
    /// </para>
    /// </summary>
    private async Task<int> ResolveSetVersionAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var keyCount = await _dbContext.EntitlementSigningKeys
            .AsNoTracking()
            .CountAsync(k => k.TenantId == tenantId, cancellationToken);

        var withdrawn = await _dbContext.EntitlementSigningKeys
            .AsNoTracking()
            .CountAsync(
                k => k.TenantId == tenantId && k.Status == EntitlementKeyStatuses.Withdrawn,
                cancellationToken);

        // Every generation raises it; every withdrawal raises it again. Both events change the set, and
        // both must produce a version an old cached set loses to.
        return keyCount + withdrawn;
    }

    /// <summary>
    /// The root key: long-lived, used rarely, signs nothing but key sets.
    ///
    /// <para>
    /// Held in configuration through the same protector as signing keys. Its own compromise is out of scope and
    /// handled the way it always is — a client update — which is acceptable precisely because it is used so
    /// rarely and can be held far more carefully than a signing key used every few days.
    /// </para>
    /// </summary>
    private async Task<byte[]> SignWithRootAsync(
        Guid tenantId, byte[] payload, CancellationToken cancellationToken)
    {
        var root = await _dbContext.EntitlementSigningKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId && k.Status == EntitlementKeyStatuses.Active)
            .OrderBy(k => k.NotBefore)
            .FirstOrDefaultAsync(cancellationToken);

        if (root is null || string.IsNullOrEmpty(_options.ProtectedRootKey))
        {
            // No configured root: the set is signed with the oldest active signing key as a bootstrap.
            // Weaker than a true root — a compromise of that key compromises the set — and stated
            // rather than hidden: a deployment that wants the §6 guarantee configures Entitlements:RootKey.
            if (root is null)
            {
                return new byte[64];
            }

            var bootstrap = Convert.FromBase64String(_protector.Unprotect(root.ProtectedPrivateKey));
            return _signer.Sign(payload, bootstrap);
        }

        var rootKey = Convert.FromBase64String(_protector.Unprotect(_options.ProtectedRootKey));
        return _signer.Sign(payload, rootKey);
    }

    private static string BuildKid(DateTime now)
        => $"{now:yyyy-MM}-{Guid.NewGuid().ToString("N")[..6]}";

    private static EntitlementKeyDescriptor Describe(EntitlementSigningKey key)
        => new(key.Kid, key.PublicKey, key.NotBefore, key.SigningNotAfter, key.VerifyNotAfter);
}

public sealed class EntitlementTokenOptions
{
    public const string SectionName = "Entitlements";

    /// <summary>Days a token stays fresh. Short — days, not months (§7).</summary>
    public int ExpiryDays { get; set; } = 7;

    /// <summary>Days of grace after expiry, for the day the network is gone (§8).</summary>
    public int GraceDays { get; set; } = 30;

    /// <summary>Days a signing key issues tokens before rotating.</summary>
    public int SigningLifetimeDays { get; set; } = 90;

    /// <summary>The protected root private key. Optional; without it the set is bootstrap-signed.</summary>
    public string? ProtectedRootKey { get; set; }
}
