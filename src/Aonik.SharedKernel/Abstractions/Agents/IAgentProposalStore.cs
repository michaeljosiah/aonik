namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Cross-module write surface for agent proposals. Domain modules that need
/// to create / approve / reject proposals (currently Finance's
/// FinancialLifeGraph inference service) call this contract instead of
/// touching the Agents persistence directly. Implemented inside the
/// Agents runtime — keeps the entity, DbContext, and tenant filters
/// internal to that module.
/// </summary>
public interface IAgentProposalStore
{
    /// <summary>
    /// Bulk-add proposals and persist them in a single SaveChanges. The caller
    /// supplies the deterministic <see cref="AgentProposalCreateRequest.Id"/>
    /// per record so subsequent lookups (and idempotent re-runs) line up.
    /// </summary>
    Task CreateManyAsync(
        IReadOnlyList<AgentProposalCreateRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the proposal for the given ID, or null when missing. Tenant
    /// scoped via the implementation's query filters — callers receive
    /// proposals for the current tenant only.
    /// </summary>
    Task<AgentProposalDetail?> GetByIdAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all proposals in the <c>Proposed</c> status for the current
    /// tenant, optionally filtered by <paramref name="proposalType"/>.
    /// Ordered by CreatedAt ascending.
    /// </summary>
    Task<IReadOnlyList<AgentProposalDetail>> ListProposedAsync(
        string? proposalType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a proposal as approved. The implementation stamps the current
    /// user + UTC clock onto <c>ApprovedByUserId</c> / <c>ApprovedAt</c>.
    /// Throws when the proposal is not in <c>Proposed</c> status.
    /// </summary>
    Task ApproveAsync(
        Guid proposalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a proposal as rejected. When <paramref name="reason"/> is
    /// supplied the implementation appends it to <c>ImpactSummary</c> so
    /// reviewers can see why. Throws when the proposal is not in
    /// <c>Proposed</c> status.
    /// </summary>
    Task RejectAsync(
        Guid proposalId,
        string? reason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input record for <see cref="IAgentProposalStore.CreateManyAsync"/>. Maps
/// 1:1 onto the persisted <c>Proposal</c> entity but keeps the entity type
/// internal to the Agents runtime.
/// </summary>
public sealed record AgentProposalCreateRequest(
    Guid Id,
    Guid TenantId,
    string ProposalType,
    Guid ProposedByAgentId,
    Guid? AiRunId,
    string ImpactSummary,
    string RiskTier,
    string PayloadJson);

/// <summary>
/// Output record for <see cref="IAgentProposalStore.GetByIdAsync"/> and
/// <see cref="IAgentProposalStore.ListProposedAsync"/>. Carries only the
/// fields domain consumers need; status is the string form of the
/// internal enum so SharedKernel does not need a copy of the enum type.
/// </summary>
public sealed record AgentProposalDetail(
    Guid Id,
    Guid TenantId,
    string ProposalType,
    string Status,
    string PayloadJson,
    string? ImpactSummary);
