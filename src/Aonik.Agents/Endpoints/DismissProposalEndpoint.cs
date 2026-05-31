using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Transitions a proposal from Proposed to Rejected and runs the
/// optional registered <see cref="Aonik.SharedKernel.Abstractions.Agents.IProposalRejectionHandler"/>
/// for the type. The dashboard surfaces this as "Dismiss" — same backend
/// transition, friendlier user verb. Returns the updated detail.
///
/// <para>Spec 030 status codes:</para>
/// <list type="bullet">
///   <item>200 — proposal dismissed; rejection handler (if any) ran successfully.</item>
///   <item>404 — proposal not found.</item>
///   <item>409 — proposal already resolved.</item>
///   <item>500 — proposal was marked Rejected but the rejection handler threw;
///         manual cleanup is required. The user's dismissal intent is preserved
///         so the row is not un-dismissed.</item>
/// </list>
/// </summary>
internal sealed class DismissProposalEndpoint : Endpoint<DismissProposalRequest, ProposalDetailResponse>
{
    private readonly IProposalApprovalService _service;
    private readonly IProposalApprovalPolicy _policy;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ITenantProvider _tenantProvider;

    public DismissProposalEndpoint(
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
        Post("/ai/proposals/{Id}/dismiss");
        // Spec 032 §8.2 — same authorization seam as the approve endpoint. A dismiss is also a
        // resolution decision, so it consults the IProposalApprovalPolicy (authenticated user +
        // tenant boundary in v1) rather than the coarse AdminUserPolicy role gate.
        Summary(s =>
        {
            s.Summary = "Dismiss (reject) an agent proposal";
            s.Description = "Transitions a Proposed proposal to Rejected and runs the registered rejection handler (if any). Returns the updated detail.";
            s.Response(200, "Dismissed");
            s.Response(401, "Not authenticated");
            s.Response(403, "Not authorized to decide this proposal");
            s.Response(404, "Proposal not found");
            s.Response(409, "Proposal already resolved");
            s.Response(500, "Dismissed but rejection cleanup failed");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(DismissProposalRequest req, CancellationToken ct)
    {
        // Spec 032 §8.2 authorization seam (replaces the AdminUserPolicy role gate):
        //   401 — no authenticated user; 404 — not visible in this tenant (query filter);
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
            var updated = await _service.DismissAsync(req.Id, ct);
            await Send.OkAsync(updated, ct);
        }
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
        catch (Exception ex)
        {
            // Spec 030 §5.6: when the rejection handler throws, the row has
            // already moved to Rejected (we don't un-dismiss the user's
            // intent). Surface as 500 so the failure is operationally loud,
            // with a structured body that says cleanup failed and points the
            // operator at the proposal id.
            AddError(
                $"Proposal {req.Id} was dismissed but the rejection cleanup handler failed: {ex.Message}. The proposal status is Rejected; domain-side cleanup may require manual intervention.",
                errorCode: "proposal_rejection_cleanup_failed");
            await Send.ErrorsAsync(StatusCodes.Status500InternalServerError, ct);
        }
    }
}

public sealed record DismissProposalRequest
{
    public Guid Id { get; init; }
}
