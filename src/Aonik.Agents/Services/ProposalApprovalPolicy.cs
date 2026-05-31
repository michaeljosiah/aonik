using Aonik.SharedKernel.Abstractions.Agents;

namespace Aonik.Agents.Services;

/// <summary>
/// Default <see cref="IProposalApprovalPolicy"/> (Spec 032 §8.2). v1 enforces the two rules that
/// hold regardless of product surface:
/// <list type="number">
///   <item>an authenticated user must be making the decision;</item>
///   <item>the actor's tenant must match the proposal's tenant.</item>
/// </list>
/// The tenant rule is also structurally enforced upstream — the AgentsDbContext query filter makes
/// a cross-tenant proposal invisible (the endpoint 404s before it ever reaches here) — so this is a
/// defence-in-depth restatement of that invariant in the one place future rules will live.
/// <para>
/// Deferred (documented, not yet wired): consumer self-approval (approver == originating user),
/// B2B separation-of-duties, and risk-tier step-up. Each needs the <c>Proposal</c> to persist an
/// originating-user id, which is out of the focused slice.
/// </para>
/// </summary>
internal sealed class ProposalApprovalPolicy : IProposalApprovalPolicy
{
    public ApprovalAuthorization Authorize(ApprovalActor actor, ProposalAuthorizationContext context)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);

        if (actor.UserId is null)
        {
            return ApprovalAuthorization.Denied("No authenticated user is making the decision.");
        }

        if (actor.TenantId != context.TenantId)
        {
            return ApprovalAuthorization.Denied("The proposal belongs to a different tenant.");
        }

        // v1: any authenticated same-tenant user may decide. Self-approval / SoD / step-up land
        // here once the proposal carries an originating-user id (Spec 032 §8.2).
        return ApprovalAuthorization.Allowed;
    }
}
