using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;

using FastEndpoints;

using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Records a server-validated decision for a pending tool-approval request (Spec 032 §7.5). This is
/// the single front door a Medium confirm or a High in-session approval arrives through, regardless
/// of transport. The endpoint is deliberately thin: it rejects an unauthenticated caller, parses the
/// decision, and delegates to <see cref="IToolApprovalService.DecideAsync"/> — which is the actual
/// decision authority. No transport decides whether a mutation runs; identity, tenant, freshness,
/// expiry, single-use status, and (for High) the proposal-approval policy + dispatch are all
/// enforced inside the service.
///
/// <para>Status codes:</para>
/// <list type="bullet">
///   <item>200 — the request was approved or rejected and recorded.</item>
///   <item>400 — the body's decision value was not "Approve" or "Reject".</item>
///   <item>401 — no authenticated user is making the decision.</item>
///   <item>403 — the caller may not decide this request (not the requesting user / policy refused).</item>
///   <item>404 — no request (or, for High, no linked proposal) with that id is visible in this tenant.</item>
///   <item>409 — the request expired, was already decided, or the linked proposal is no longer
///         in a state that can be approved (<see cref="NoProposalHandlerRegisteredException"/> /
///         <see cref="InvalidOperationException"/>).</item>
///   <item>422 — a High approval's handler refused to apply for a business reason
///         (<see cref="ProposalExecutionFailedException"/>); the action did not run.</item>
/// </list>
/// </summary>
internal sealed class DecideToolApprovalEndpoint
    : Endpoint<DecideToolApprovalRequest, DecideToolApprovalResponse>
{
    private readonly IToolApprovalService _service;
    private readonly ICurrentUserProvider _currentUserProvider;

    public DecideToolApprovalEndpoint(
        IToolApprovalService service,
        ICurrentUserProvider currentUserProvider)
    {
        _service = service;
        _currentUserProvider = currentUserProvider;
    }

    public override void Configure()
    {
        Post("/ai/tool-approvals/{Id}/decide");
        // Authenticated like the proposal approve/dismiss endpoints: with no global fallback policy
        // the framework allows anonymous, so HandleAsync rejects a missing user with 401 before any
        // decision is read. The fine-grained authorisation (requesting-user equality, tenant, policy)
        // lives in DecideAsync so it is enforced no matter which transport delivers the decision.
        Summary(s =>
        {
            s.Summary = "Decide a pending tool-approval request";
            s.Description =
                "Approves or rejects a Medium-tier confirm, or routes a High-tier in-session approval " +
                "through the proposal-approval path. The decision is validated server-side for identity, " +
                "tenant, expiry, and single-use status before it has any effect.";
            s.Response(200, "Decision recorded");
            s.Response(400, "Invalid decision value");
            s.Response(401, "Not authenticated");
            s.Response(403, "Not authorized to decide this request");
            s.Response(404, "Request not found");
            s.Response(409, "Request expired, already decided, or proposal not approvable");
            s.Response(422, "High approval handler refused to apply");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(DecideToolApprovalRequest req, CancellationToken ct)
    {
        // No authenticated user ⇒ no authority to decide. DecideAsync also guards this (defense in
        // depth), but we short-circuit with a 401 here rather than a 403 to match the proposal endpoints.
        if (!_currentUserProvider.TryGetCurrentUserId(out _))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        if (!Enum.TryParse<ToolApprovalDecisionType>(req.Decision, ignoreCase: true, out var decisionType))
        {
            AddError(
                $"'{req.Decision}' is not a valid decision. Use 'Approve' or 'Reject'.",
                errorCode: "invalid_decision");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        try
        {
            var result = await _service.DecideAsync(
                req.Id,
                new ToolApprovalDecisionInput(decisionType, req.Reason),
                ct);

            switch (result.Outcome)
            {
                case ToolApprovalDecisionOutcome.Approved:
                case ToolApprovalDecisionOutcome.Rejected:
                    await Send.OkAsync(
                        new DecideToolApprovalResponse(
                            result.ApprovalRequestId,
                            result.Outcome.ToString(),
                            result.ProposalId,
                            result.Message),
                        ct);
                    return;

                case ToolApprovalDecisionOutcome.Forbidden:
                    AddError(result.Message ?? "You are not authorized to decide this request.");
                    await Send.ErrorsAsync(StatusCodes.Status403Forbidden, ct);
                    return;

                case ToolApprovalDecisionOutcome.NotFound:
                    await Send.NotFoundAsync(ct);
                    return;

                case ToolApprovalDecisionOutcome.Expired:
                case ToolApprovalDecisionOutcome.AlreadyDecided:
                default:
                    AddError(result.Message ?? "This request can no longer be decided.");
                    await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
                    return;
            }
        }
        // A High approval runs the proposal through IProposalApprovalService.ApproveAsync, which can
        // throw the same execution exceptions the proposal-approval endpoint maps. Mirror that mapping
        // so an in-session High approval and a queue approval surface identical failures.
        catch (KeyNotFoundException)
        {
            await Send.NotFoundAsync(ct);
        }
        catch (NoProposalHandlerRegisteredException ex)
        {
            AddError(ex.Message, errorCode: "no_proposal_handler_registered");
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
        catch (ProposalExecutionFailedException ex)
        {
            AddError(ex.Message, errorCode: "proposal_execution_failed");
            await Send.ErrorsAsync(StatusCodes.Status422UnprocessableEntity, ct);
        }
        catch (InvalidOperationException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(StatusCodes.Status409Conflict, ct);
        }
    }
}

/// <summary>
/// Decision payload. <see cref="Id"/> binds from the route; <see cref="Decision"/> ("Approve" or
/// "Reject") and the optional <see cref="Reason"/> bind from the body.
/// </summary>
public sealed record DecideToolApprovalRequest
{
    public Guid Id { get; init; }

    public string Decision { get; init; } = string.Empty;

    public string? Reason { get; init; }
}

/// <summary>Result of a tool-approval decision, surfaced to the deciding client.</summary>
public sealed record DecideToolApprovalResponse(
    Guid ApprovalRequestId,
    string Outcome,
    Guid? ProposalId,
    string? Message);
