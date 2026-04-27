using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Transitions a proposal from Proposed to Rejected. The dashboard
/// surfaces this as "Dismiss" — same backend transition, friendlier
/// user verb. Returns the updated detail.
/// </summary>
internal sealed class DismissProposalEndpoint : Endpoint<DismissProposalRequest, ProposalDetailResponse>
{
    private readonly IProposalApprovalService _service;

    public DismissProposalEndpoint(IProposalApprovalService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/ai/proposals/{Id}/dismiss");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Dismiss (reject) an agent proposal";
            s.Description = "Transitions a Proposed proposal to Rejected. Returns the updated detail.";
            s.Response(200, "Dismissed");
            s.Response(401, "Not authenticated");
            s.Response(404, "Proposal not found");
            s.Response(409, "Proposal already resolved");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(DismissProposalRequest req, CancellationToken ct)
    {
        try
        {
            var detail = await _service.DismissAsync(req.Id, ct);
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

public sealed record DismissProposalRequest
{
    public Guid Id { get; init; }
}
