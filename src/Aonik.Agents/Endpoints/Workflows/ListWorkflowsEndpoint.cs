using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class ListWorkflowsEndpoint
    : EndpointWithoutRequest<IReadOnlyList<WorkflowSummaryResponse>>
{
    private readonly IWorkflowService _service;

    public ListWorkflowsEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/workflows");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List workflows";
            s.Description = "Returns all workflows visible to the current tenant, with the inline step rail + 24h KPI aggregates the registry page uses.";
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var workflows = await _service.ListAsync(ct);
        await Send.OkAsync(workflows, ct);
    }
}
