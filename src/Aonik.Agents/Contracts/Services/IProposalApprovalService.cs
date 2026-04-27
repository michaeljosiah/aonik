using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Mutates the <c>Proposal</c> status lifecycle (Proposed → Approved or
/// Proposed → Rejected) and exposes a single read by id for the Review
/// dialog. Tenant scoping is enforced by the AgentsDbContext query
/// filter; the service stamps <c>ApprovedByUserId</c> + <c>ApprovedAt</c>
/// with the calling user / clock on every transition.
/// </summary>
public interface IProposalApprovalService
{
    /// <summary>Returns full detail for a single proposal, or null if not found / not in this tenant.</summary>
    Task<ProposalDetailResponse?> GetByIdAsync(Guid proposalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns pending (Status == Proposed) proposals for the current
    /// tenant, optionally filtered by ProposalType, AgentDomain, or
    /// RiskTier. Sorted newest-first. The Approvals queue UI is the
    /// primary consumer.
    /// </summary>
    Task<ListProposalsResponse> ListPendingAsync(
        ListProposalsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a Proposed proposal to Approved. Throws
    /// <see cref="InvalidOperationException"/> if the proposal is already
    /// resolved (Approved/Rejected). Returns the updated detail.
    /// </summary>
    Task<ProposalDetailResponse> ApproveAsync(Guid proposalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a Proposed proposal to Rejected. The dashboard surfaces
    /// this as "Dismiss" — same backend transition, friendlier user verb.
    /// </summary>
    Task<ProposalDetailResponse> DismissAsync(Guid proposalId, CancellationToken cancellationToken = default);
}
