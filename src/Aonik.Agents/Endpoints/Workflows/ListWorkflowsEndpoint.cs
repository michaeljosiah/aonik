using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record ListWorkflowsRequest(int? Skip, int? Take);

internal sealed class ListWorkflowsEndpoint
    : Endpoint<ListWorkflowsRequest, IReadOnlyList<WorkflowSummaryResponse>>
{
    private readonly IWorkflowService _service;

    public ListWorkflowsEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/workflows");
        Summary(s =>
        {
            s.Summary = "List workflows";
            s.Description = "Returns all workflows visible to the current tenant, with the inline step rail + 24h KPI aggregates the registry page uses.";
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(ListWorkflowsRequest req, CancellationToken ct)
    {
        var workflows = await _service.ListAsync(req.Skip ?? 0, req.Take ?? 50, ct);
        await Send.OkAsync(workflows, ct);
    }
}
