using Aonik.Agents.Entities;
using Aonik.Agents.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Agents.Services;

/// <summary>
/// <see cref="IToolApprovalRequestStore"/> backed by <see cref="AgentsDbContext"/>. The context's
/// tenant query filter scopes every read to the current tenant, so a cross-tenant id simply returns
/// null — the structural enforcement behind the §12 "tenant on every decision" rule.
/// </summary>
internal sealed class ToolApprovalRequestStore : IToolApprovalRequestStore
{
    private readonly AgentsDbContext _agentsDbContext;

    public ToolApprovalRequestStore(AgentsDbContext agentsDbContext)
    {
        _agentsDbContext = agentsDbContext;
    }

    public async Task CreateAsync(ToolApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        _agentsDbContext.ToolApprovalRequests.Add(request);
        await _agentsDbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ToolApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _agentsDbContext.ToolApprovalRequests
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<ToolApprovalRequest?> FindConsumableApprovedAsync(
        Guid tenantId,
        Guid? requestingUserId,
        string toolName,
        string argsHash,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        _agentsDbContext.ToolApprovalRequests
            .Where(r => r.TenantId == tenantId
                && r.RequestingUserId == requestingUserId
                && r.ToolName == toolName
                && r.ArgsHash == argsHash
                && r.Status == ToolApprovalRequestStatus.Approved
                && r.ConsumedAt == null
                && r.ExpiresAt > nowUtc)
            .OrderBy(r => r.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _agentsDbContext.SaveChangesAsync(cancellationToken);
}
