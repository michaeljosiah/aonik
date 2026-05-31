namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// The actor making an approve / dismiss decision on a proposal. <see cref="UserId"/> is null
/// when no authenticated user is present; <see cref="TenantId"/> is the actor's current tenant.
/// </summary>
public sealed record ApprovalActor(Guid? UserId, Guid TenantId);

/// <summary>
/// What is being decided: the proposal's identity, owning tenant, type, risk tier, and — once the
/// schema carries it — the user the proposal was raised on behalf of (<see cref="OriginatingUserId"/>,
/// null in the focused slice).
/// </summary>
public sealed record ProposalAuthorizationContext(
    Guid ProposalId,
    Guid TenantId,
    string ProposalType,
    string? RiskTier,
    Guid? OriginatingUserId);

/// <summary>
/// Outcome of an <see cref="IProposalApprovalPolicy"/> evaluation. <see cref="IsAuthorized"/> gates
/// the decision; <see cref="Reason"/> explains a denial (surfaced as the 403 body).
/// </summary>
public sealed record ApprovalAuthorization(bool IsAuthorized, string? Reason)
{
    /// <summary>The decision is permitted.</summary>
    public static ApprovalAuthorization Allowed { get; } = new(true, null);

    /// <summary>The decision is refused for the given reason.</summary>
    public static ApprovalAuthorization Denied(string reason) => new(false, reason);
}

/// <summary>
/// Spec 032 §8.2 — decides whether an <see cref="ApprovalActor"/> may approve or dismiss a given
/// proposal. This replaces the coarse <c>AdminUserPolicy</c> role gate on the proposal
/// decision endpoints with a single, testable seam where the real authorization rules live.
/// <para>
/// Focused slice (v1): the rule set is the mandatory tenant boundary plus an authenticated-user
/// requirement. Consumer self-approval (the approver must equal the originating user), B2B
/// separation-of-duties, and risk-tier step-up are deferred until the <c>Proposal</c> persists an
/// originating-user id — but they will land here, not in the endpoint or the service.
/// </para>
/// </summary>
public interface IProposalApprovalPolicy
{
    /// <summary>Returns whether <paramref name="actor"/> may decide the proposal described by <paramref name="context"/>.</summary>
    ApprovalAuthorization Authorize(ApprovalActor actor, ProposalAuthorizationContext context);
}
