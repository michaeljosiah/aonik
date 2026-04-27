using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Transitions a proposal from Proposed to Approved, stamping the
/// approving user and the timestamp. Returns the updated detail so the
/// frontend can replace the pending card without an extra GET.
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
            s.Description = "Transitions a Proposed proposal to Approved. Returns the updated detail.";
            s.Response(200, "Approved");
            s.Response(401, "Not authenticated");
            s.Response(404, "Proposal not found");
            s.Response(409, "Proposal already resolved");
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
