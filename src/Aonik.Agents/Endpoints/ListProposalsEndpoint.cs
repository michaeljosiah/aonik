using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints;

/// <summary>
/// Returns pending agent proposals for the Approvals queue UI. Filterable by
/// proposal type, agent domain, or risk tier so the queue's left rail can
/// scope the list without N round trips.
/// </summary>
internal sealed class ListProposalsEndpoint : EndpointWithoutRequest<ListProposalsResponse>
{
    private readonly IProposalApprovalService _service;

    public ListProposalsEndpoint(IProposalApprovalService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/proposals");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List pending agent proposals";
            s.Description =
                "Returns up to `take` (default 100, max 500) pending proposals for the current tenant, " +
                "newest first. Optional filters: proposalType, agentDomain, riskTier.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("AI Agents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var request = new ListProposalsRequest(
            ProposalType: Query<string?>("proposalType", isRequired: false),
            AgentDomain: Query<string?>("agentDomain", isRequired: false),
            RiskTier: Query<string?>("riskTier", isRequired: false),
            Take: Query<int?>("take", isRequired: false) ?? 100);

        var result = await _service.ListPendingAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
