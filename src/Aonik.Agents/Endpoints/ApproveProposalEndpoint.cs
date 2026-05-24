using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;
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

    public ApproveProposalEndpoint(IProposalApprovalService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/ai/proposals/{Id}/approve");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Approve an agent proposal";
            s.Description = "Transitions a Proposed proposal to Approved and runs the registered handler for the proposal type. Returns the updated detail with applied-resource metadata.";
            s.Response(200, "Approved and applied");
            s.Response(401, "Not authenticated");
            s.Response(404, "Proposal not found");
            s.Response(409, "Proposal already resolved or no handler registered for type");
            s.Response(422, "Handler refused to apply for a business reason");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(ApproveProposalRequest req, CancellationToken ct)
    {
        try
        {
            var detail = await _service.ApproveAsync(req.Id, ct);
            await Send.OkAsync(detail, ct);
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
