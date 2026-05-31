using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Transitions a proposal from Proposed to Approved, runs the registered
/// <see cref="IProposalHandler"/> for the type, and returns the updated
/// detail. The response includes the handler's applied-resource metadata
/// so the frontend can describe what changed without a follow-up GET.
///
/// <para>Spec 030 status codes:</para>
/// <list type="bullet">
///   <item>200 — proposal approved and the handler reported Applied = true.</item>
///   <item>404 — proposal not found.</item>
///   <item>409 — proposal already resolved, or no handler is registered for
///         the proposal type (<see cref="NoProposalHandlerRegisteredException"/>).</item>
///   <item>422 — handler returned Applied = false for an expected business
///         reason (<see cref="ProposalExecutionFailedException"/>); proposal
///         is left in Proposed.</item>
/// </list>
/// </summary>
internal sealed class ApproveProposalEndpoint : Endpoint<ApproveProposalRequest, ProposalDetailResponse>
{
    private readonly IProposalApprovalService _service;
    private readonly IProposalApprovalPolicy _policy;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantProvider _tenantProvider;

    public ApproveProposalEndpoint(
        IProposalApprovalService service,
        IProposalApprovalPolicy policy,
        ICurrentUserProvider currentUserProvider,
        ITenantProvider tenantProvider)
    {
        _service = service;
        _policy = policy;
        _currentUserProvider = currentUserProvider;
        _tenantProvider = tenantProvider;
    }

    public override void Configure()
    {
        Post("/ai/proposals/{Id}/approve");
        // Spec 032 §8.2 — the coarse "AdminUserPolicy" role gate is replaced by the
        // IProposalApprovalPolicy seam evaluated in HandleAsync (authenticated user + tenant
        // boundary in v1; self/SoD/step-up later). The endpoint stays authenticated: with no
        // global fallback policy the framework allows anonymous, so HandleAsync rejects a
        // missing user with 401 before any proposal is read.
        Summary(s =>
        {
            s.Summary = "Approve an agent proposal";
            s.Description = "Transitions a Proposed proposal to Approved and runs the registered handler for the proposal type. Returns the updated detail with applied-resource metadata.";
            s.Response(200, "Approved and applied");
            s.Response(401, "Not authenticated");
            s.Response(403, "Not authorized to decide this proposal");
            s.Response(404, "Proposal not found");
            s.Response(409, "Proposal already resolved or no handler registered for type");
            s.Response(422, "Handler refused to apply for a business reason");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ApproveProposalRequest req, CancellationToken ct)
    {
        // Spec 032 §8.2 authorization seam (replaces the AdminUserPolicy role gate):
        //   401 — no authenticated user is making the decision;
        //   404 — the proposal is not visible in this tenant (structurally enforced by the
        //         AgentsDbContext query filter — GetByIdAsync returns null cross-tenant);
        //   403 — the IProposalApprovalPolicy refuses this actor.
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var detail = await _service.GetByIdAsync(req.Id, ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // The proposal was loaded through the tenant query filter, so its owning tenant equals
        // the current tenant; passing the current tenant for both sides of the policy's tenant
        // check is the documented belt-and-suspenders restatement of that invariant.
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var authorization = _policy.Authorize(
            new ApprovalActor(userId, tenantId),
            new ProposalAuthorizationContext(
                detail.Id,
                tenantId,
                detail.ProposalType,
                detail.RiskTier,
                OriginatingUserId: null));
        if (!authorization.IsAuthorized)
        {
            AddError(authorization.Reason ?? "You are not authorized to decide this proposal.");
            await Send.ErrorsAsync(StatusCodes.Status403Forbidden, ct);
            return;
        }

        try
        {
            var updated = await _service.ApproveAsync(req.Id, ct);
            await Send.OkAsync(updated, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (NoProposalHandlerRegisteredException ex)
        {
            AddError(
                ex.Message,
                errorCode: "no_proposal_handler_registered");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
        catch (ProposalExecutionFailedException ex)
        {
            AddError(
                ex.Message,
                errorCode: "proposal_execution_failed");
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}

public sealed record ApproveProposalRequest
{
    public Guid Id { get; init; }
}
