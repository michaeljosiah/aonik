using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Entitlements;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Aonik.Subscriptions.Entities.Entitlements;
using Aonik.Subscriptions.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Subscriptions.Services.Entitlements;

/// <summary>
/// Issues, refreshes and revokes entitlement tokens (Spec 090 §7–§9).
/// </summary>
internal sealed class EntitlementTokenIssuer : IEntitlementTokenIssuer
{
    private readonly SubscriptionsDbContext _dbContext;
    private readonly IEntitlementReader _entitlements;
    private readonly IEntitlementKeyRing _keyRing;
    private readonly IEd25519Signer _signer;
    private readonly IEntitlementKeyProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly EntitlementTokenOptions _options;
    private readonly ILogger<EntitlementTokenIssuer> _logger;

    public EntitlementTokenIssuer(
        SubscriptionsDbContext dbContext,
        IEntitlementReader entitlements,
        IEntitlementKeyRing keyRing,
        IEd25519Signer signer,
        IEntitlementKeyProtector protector,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<EntitlementTokenOptions> options,
        ILogger<EntitlementTokenIssuer> logger)
    {
        _dbContext = dbContext;
        _entitlements = entitlements;
        _keyRing = keyRing;
        _signer = signer;
        _protector = protector;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IssuedEntitlementToken> IssueAsync(
        SubscriberRef subscriber,
        string? deviceFingerprint = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var snapshot = await _entitlements.GetAsync(subscriber, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Subscriber {subscriber.Kind}:{subscriber.Id} has no entitlements to project.");

        var signingKey = await _keyRing.GetSigningKeyAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "No entitlement signing key is active. Rotate first; issuing never generates one implicitly.");

        var jti = Guid.NewGuid();
        var expiresAt = now.AddDays(_options.ExpiryDays);
        var graceUntil = expiresAt.AddDays(_options.GraceDays);

        // §6.1, enforced at ISSUE time and atomically. Checking VerifyNotAfter only when it is written
        // says nothing about tokens signed afterwards: the moment someone lengthens the grace window —
        // an ordinary, well-intentioned configuration change — a later token could carry a GraceUntil
        // beyond the stored deadline, and a retirement computed yesterday would silently invalidate a
        // token issued today, during its grace, offline, for a paying customer. The guarded update
        // extends VerifyNotAfter in the same statement that observes it, so retirement cannot read the
        // old bound while this issuance commits the new one.
        var extended = await _dbContext.EntitlementSigningKeys
            .Where(k => k.TenantId == tenantId
                && k.Kid == signingKey.Kid
                && k.Status != EntitlementKeyStatuses.Withdrawn)
            .ExecuteUpdateAsync(
                s => s.SetProperty(
                    k => k.VerifyNotAfter,
                    k => k.VerifyNotAfter < graceUntil ? graceUntil : k.VerifyNotAfter),
                cancellationToken);

        if (extended == 0)
        {
            // Withdrawn between selection and issue — the compromise path. Refusing is correct: a token
            // signed now would fail at every verifier that has fetched the new set.
            throw new InvalidOperationException(
                $"Signing key {signingKey.Kid} was withdrawn during issuance; retry to pick up its successor.");
        }

        // The per-subscriber revocation handle: random, rotated on each refresh, and what lets
        // subscriber-wide revocation be published without publishing who was revoked. Only a holder of
        // the token can compute the handle to look up.
        var revocationHandle = EntitlementTokenFormat.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));

        var payload = BuildPayload(
            snapshot, subscriber, tenantId, jti, revocationHandle,
            now, expiresAt, graceUntil, deviceFingerprint, signingKey.Kid);

        var privateKey = Convert.FromBase64String(
            _protector.Unprotect(await ProtectedKeyForAsync(tenantId, signingKey.Kid, cancellationToken)));

        var token = EntitlementTokenFormat.Compose(payload, bytes => _signer.Sign(bytes, privateKey));

        _dbContext.EntitlementTokenIssues.Add(new EntitlementTokenIssue
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            SubscriberKind = subscriber.Kind,
            SubscriberId = subscriber.Id,
            Jti = jti,
            Kid = signingKey.Kid,
            RevocationHandle = revocationHandle,
            DeviceFingerprint = deviceFingerprint,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            // Persisted, not derived — the §6.1 invariant is MAX(gra) per kid, and gra is
            // tenant-configurable after signing. Store the value that was actually signed.
            GraceUntil = graceUntil,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedEntitlementToken(token, jti, signingKey.Kid, expiresAt, graceUntil);
    }

    /// <summary>
    /// The payload, serialised once. The bytes produced here are the bytes that are signed and the bytes that
    /// are transmitted — nothing downstream re-serialises, which is what makes canonicalisation unnecessary.
    /// </summary>
    private byte[] BuildPayload(
        EntitlementSnapshot snapshot,
        SubscriberRef subscriber,
        Guid tenantId,
        Guid jti,
        string revocationHandle,
        DateTime now,
        DateTime expiresAt,
        DateTime graceUntil,
        string? deviceFingerprint,
        string kid)
    {
        var flags = snapshot.Meters
            .Where(m => string.Equals(m.Kind, MeterKinds.Flag, StringComparison.OrdinalIgnoreCase))
            .Select(m => m.MeterCode)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

        var ceilings = snapshot.Meters
            .Where(m => string.Equals(m.Kind, MeterKinds.Ceiling, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.MeterCode, StringComparer.Ordinal)
            .ToList();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("v", 1);
            writer.WriteString("jti", jti.ToString("D"));
            writer.WriteString("rvh", revocationHandle);
            writer.WriteString("sub", $"{subscriber.Kind}:{subscriber.Id:D}");
            writer.WriteString("tid", tenantId.ToString("D"));
            writer.WriteString("plan", snapshot.PlanCode);

            writer.WriteStartArray("feat");
            foreach (var flag in flags)
            {
                writer.WriteStringValue(flag);
            }
            writer.WriteEndArray();

            writer.WriteStartObject("lim");
            foreach (var ceiling in ceilings)
            {
                writer.WriteNumber(ceiling.MeterCode, (long)decimal.Truncate(ceiling.Allowance));
            }
            writer.WriteEndObject();

            // Advisory snapshot of usage across all devices at the moment of issue (§5.2). Never
            // authoritative: it may make the client refuse or warn EARLIER, and may never permit
            // anything the server has not confirmed.
            writer.WriteStartObject("use");
            foreach (var ceiling in ceilings)
            {
                writer.WriteNumber(ceiling.MeterCode, (long)decimal.Truncate(ceiling.Held));
            }
            writer.WriteEndObject();

            // Integer seconds since the Unix epoch, UTC. Never strings, never milliseconds.
            writer.WriteNumber("iat", new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeSeconds());
            writer.WriteNumber("exp", new DateTimeOffset(expiresAt, TimeSpan.Zero).ToUnixTimeSeconds());
            writer.WriteNumber("gra", new DateTimeOffset(graceUntil, TimeSpan.Zero).ToUnixTimeSeconds());

            if (!string.IsNullOrWhiteSpace(deviceFingerprint))
            {
                writer.WriteString("dev", deviceFingerprint);
            }

            writer.WriteString("kid", kid);
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public async Task<bool> RevokeAsync(
        Guid? jti,
        SubscriberRef? subscriber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        if (jti is { } tokenId)
        {
            var issue = await _dbContext.EntitlementTokenIssues
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Jti == tokenId, cancellationToken);

            if (issue is null)
            {
                return false;
            }

            _dbContext.EntitlementRevocations.Add(new EntitlementRevocation
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Jti = tokenId,
                Reason = reason,
                AddedAt = now,
                // Sweepable once the token it names has left grace — after that the verifier rejects it
                // on time alone and the entry is dead weight in a public list.
                SweepAfter = issue.GraceUntil,
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (subscriber is { } who)
        {
            // Every live handle for the subscriber. Published as handles, the list names nobody: only a
            // holder of a token can compute its own handle to look up.
            var issues = await _dbContext.EntitlementTokenIssues
                .AsNoTracking()
                .Where(i => i.TenantId == tenantId
                    && i.SubscriberKind == who.Kind
                    && i.SubscriberId == who.Id
                    && i.GraceUntil > now)
                .Select(i => new { i.RevocationHandle, i.GraceUntil })
                .ToListAsync(cancellationToken);

            if (issues.Count == 0)
            {
                return false;
            }

            foreach (var issue in issues.DistinctBy(i => i.RevocationHandle))
            {
                _dbContext.EntitlementRevocations.Add(new EntitlementRevocation
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RevocationHandle = issue.RevocationHandle,
                    Reason = reason,
                    AddedAt = now,
                    SweepAfter = issue.GraceUntil,
                });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }

        return false;
    }

    public async Task<EntitlementRevocationList> GetRevocationsAsync(
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var live = await _dbContext.EntitlementRevocations
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.SweepAfter > now)
            .Select(r => new { r.Jti, r.RevocationHandle })
            .ToListAsync(cancellationToken);

        return new EntitlementRevocationList(
            [.. live.Where(r => r.Jti != null).Select(r => r.Jti!.Value)],
            [.. live.Where(r => r.RevocationHandle != null).Select(r => r.RevocationHandle!)]);
    }

    private async Task<string> ProtectedKeyForAsync(
        Guid tenantId, string kid, CancellationToken cancellationToken)
        => await _dbContext.EntitlementSigningKeys
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId && k.Kid == kid)
            .Select(k => k.ProtectedPrivateKey)
            .FirstAsync(cancellationToken);
}
