using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record ListWorkflowRunsRequest(Guid WorkflowId, int? Take);

internal sealed class ListWorkflowRunsEndpoint
    : Endpoint<ListWorkflowRunsRequest, IReadOnlyList<WorkflowRunResponse>>
{
    private readonly IWorkflowService _service;

    public ListWorkflowRunsEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/workflows/{WorkflowId:guid}/runs");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List recent runs for a workflow";
            s.Description = "Most-recent first. The detail-rail recent-runs list reads the top six, and the editor's trace replay walks the full sequence.";
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(ListWorkflowRunsRequest req, CancellationToken ct)
    {
        var runs = await _service.ListRunsAsync(req.WorkflowId, req.Take ?? 20, ct);
        await Send.OkAsync(runs, ct);
    }
}
