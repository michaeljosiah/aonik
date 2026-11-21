using System.Threading.RateLimiting;

using Aonik.SharedKernel.Abstractions.Connectors;

namespace Aonik.Platform.Services.Integrations;

/// <summary>Shared by API and agent submission. SQL uniqueness separately deduplicates across hosts.</summary>
internal sealed class ConnectorRequestRateLimiter : IDisposable
{
    private readonly PartitionedRateLimiter<(Guid TenantId, Guid UserId)> _limiter = PartitionedRateLimiter.Create<(Guid TenantId, Guid UserId), (Guid, Guid)>(
        key => RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
        { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), SegmentsPerWindow = 6, QueueLimit = 0 }));
    public void Require(Guid tenantId, Guid userId)
    {
        using RateLimitLease lease = _limiter.AttemptAcquire((tenantId, userId));
        if (!lease.IsAcquired) throw new ConnectorAccessException("RateLimited", "Too many new connection requests. Try again in a minute.");
    }
    public void Dispose() => _limiter.Dispose();
}
