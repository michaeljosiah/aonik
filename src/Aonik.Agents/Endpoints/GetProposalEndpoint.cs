using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Returns the full detail of a single agent proposal — used by the
/// dashboard's Review dialog to show the underlying payload.
/// </summary>
internal sealed class GetProposalEndpoint : Endpoint<GetProposalRequest, ProposalDetailResponse>
{
    private readonly IProposalApprovalService _service;

    public GetProposalEndpoint(IProposalApprovalService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/proposals/{Id}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get an agent proposal by id";
            s.Description = "Returns the full detail of a single agent proposal, including payload JSON, for the Review dialog.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
            s.Response(404, "Proposal not found");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(GetProposalRequest req, CancellationToken ct)
    {
        var detail = await _service.GetByIdAsync(req.Id, ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(detail, ct);
    }
}

public sealed record GetProposalRequest
{
    public Guid Id { get; init; }
}
