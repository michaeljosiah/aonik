using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZiggyCreatures.Caching.Fusion;

using Aonik.Platform.Contracts.Services.Identity;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Platform.Services.Identity;

/// <summary>
/// Spec 026 Part 3 — concrete <see cref="IUserSessionBlocklist"/>.
/// The hot path (JWT auth pipeline) reads "is this user revoked?" via
/// FusionCache; the cold path on a miss falls back to a single indexed
/// query on <c>AnkPlatformUserSessionBlocklist</c>. Writes invalidate
/// the cache so the new revoke is visible on the next request (≤ 1 TTL
/// elsewhere — FusionCache's <c>RemoveAsync</c> uses the configured
/// backplane to fan out to other replicas).
/// </summary>
internal sealed class UserSessionBlocklist : IUserSessionBlocklist
{
    private const string CacheKeyPrefix = "user-session-blocklist:";

    private readonly PlatformDbContext _dbContext;
    private readonly IFusionCache _fusionCache;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly UserLifecycleOptions _options;
    private readonly ILogger<UserSessionBlocklist> _logger;

    public UserSessionBlocklist(
        PlatformDbContext dbContext,
        IFusionCache fusionCache,
        IClock clock,
        ICurrentUserProvider currentUserProvider,
        IOptions<UserLifecycleOptions> options,
        ILogger<UserSessionBlocklist> logger)
    {
        _dbContext = dbContext;
        _fusionCache = fusionCache;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> IsRevokedAsync(
        Guid tenantId,
        Guid userId,
        DateTime tokenIssuedUtc,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            return false;
        }

        var cacheKey = BuildCacheKey(tenantId, userId);
        var ttl = TimeSpan.FromSeconds(Math.Max(5, _options.BlocklistCacheTtlSeconds));

        // Cache the most-recent revoke time per (tenant, user). Negative
        // results (no revoke) are also cached as a default-value tick so
        // we don't hit the DB on every request for unrevoked users.
        var lastRevokedTicks = await _fusionCache.GetOrSetAsync<long>(
            cacheKey,
            async (ctx, ct) =>
            {
                var lastRevoke = await _dbContext.UserSessionBlocklist
                    .AcrossTenants()
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.UserId == userId)
                    .OrderByDescending(x => x.RevokedUtc)
                    .Select(x => (DateTime?)x.RevokedUtc)
                    .FirstOrDefaultAsync(ct);

                return lastRevoke?.Ticks ?? 0L;
            },
            options => options
                .SetDuration(ttl)
                .SetJittering(TimeSpan.FromSeconds(5)),
            cancellationToken);

        if (lastRevokedTicks == 0L)
        {
            return false;
        }

        var lastRevokedUtc = new DateTime(lastRevokedTicks, DateTimeKind.Utc);
        return tokenIssuedUtc < lastRevokedUtc;
    }

    public async Task<UserSessionRevocation> RevokeAsync(
        Guid tenantId,
        Guid userId,
        Guid? revokedByUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (userId == Guid.Empty) throw new ArgumentException("User id is required.", nameof(userId));

        var now = _clock.UtcNow;
        var expiresAt = now.AddDays(Math.Max(1, _options.BlocklistRetentionDays));
        var actorId = revokedByUserId ?? _currentUserProvider.GetCurrentUserId();

        var entry = new UserSessionBlocklistEntry
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = userId,
            RevokedUtc = now,
            RevokedByUserId = actorId,
            Reason = string.IsNullOrWhiteSpace(reason) ? "operator-revoke" : reason.Trim(),
            ExpiresUtc = expiresAt,
            CreatedAt = now,
            CreatedBy = actorId,
        };

        _dbContext.UserSessionBlocklist.Add(entry);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate the per-user cache entry so the next request sees the
        // new revoke immediately. FusionCache's RemoveAsync uses the
        // configured backplane (Redis pub/sub on the multi-replica deploy)
        // so all API replicas drop the entry at the same time.
        var cacheKey = BuildCacheKey(tenantId, userId);
        await _fusionCache.RemoveAsync(cacheKey, token: cancellationToken);

        _logger.LogInformation(
            "Revoked sessions for user {UserId} in tenant {TenantId} (reason='{Reason}'); blocklist row {EntryId} expires {ExpiresUtc}",
            userId,
            tenantId,
            entry.Reason,
            entry.Id,
            expiresAt);

        return new UserSessionRevocation(
            tenantId,
            userId,
            entry.RevokedUtc,
            entry.ExpiresUtc,
            entry.RevokedByUserId,
            entry.Reason);
    }

    private static string BuildCacheKey(Guid tenantId, Guid userId)
        => $"{CacheKeyPrefix}{tenantId:N}:{userId:N}";
}
