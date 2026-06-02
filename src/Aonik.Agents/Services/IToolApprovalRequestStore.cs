using Aonik.Agents.Entities;

namespace Aonik.Agents.Services;

/// <summary>
/// Module-internal persistence for <see cref="ToolApprovalRequest"/> rows (Spec 032 §7.5). Kept
/// internal to the Agents runtime — only <see cref="ToolApprovalService"/> uses it — so the
/// entity, DbContext, and tenant filtering stay encapsulated. Reads are tenant-scoped by the
/// AgentsDbContext query filter; callers mutate the returned tracked entity and call
/// <see cref="SaveChangesAsync"/>.
/// </summary>
internal interface IToolApprovalRequestStore
{
    /// <summary>Persists a new request row.</summary>
    Task CreateAsync(ToolApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>Loads a request by id (tracked, tenant-scoped), or null if not visible in this tenant.</summary>
    Task<ToolApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the oldest Approved, unconsumed, unexpired request matching the gate's resubmit key
    /// (tenant + requesting user + tool name + args-hash), or null. Returned tracked so the gate can
    /// stamp <see cref="ToolApprovalRequest.ConsumedAt"/> and persist via <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<ToolApprovalRequest?> FindConsumableApprovedAsync(
        Guid tenantId,
        Guid? requestingUserId,
        string toolName,
        string argsHash,
        DateTime nowUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes to tracked request rows.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
