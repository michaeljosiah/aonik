using Microsoft.EntityFrameworkCore;

using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Ai.Services.Insights;

/// <summary>
/// SharedKernel implementation that exposes a daily count of <c>AiRun</c>
/// records for the current tenant. Tenant scoping is enforced both by the
/// AiDbContext query filter and an explicit predicate so the query is safe
/// when filters are bypassed.
/// </summary>
internal sealed class AiRunStatsService : IAiRunStatsService
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public AiRunStatsService(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<int> CountForTodayAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var startOfDayUtc = DateTime.UtcNow.Date;

        return await _dbContext.AiRuns
            .Where(r => r.TenantId == tenantId && r.CreatedAt >= startOfDayUtc)
            .CountAsync(cancellationToken);
    }
}
