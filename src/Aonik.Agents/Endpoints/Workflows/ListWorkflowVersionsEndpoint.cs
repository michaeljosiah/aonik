using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record ListWorkflowVersionsRequest(Guid WorkflowId);

internal sealed class ListWorkflowVersionsEndpoint
    : Endpoint<ListWorkflowVersionsRequest, IReadOnlyList<WorkflowVersionResponse>>
{
    private readonly IWorkflowService _service;

    public ListWorkflowVersionsEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/workflows/{WorkflowId:guid}/versions");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List version history for a workflow";
            s.Description = "Newest first. Powers the editor's version history sidebar.";
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(ListWorkflowVersionsRequest req, CancellationToken ct)
    {
        var versions = await _service.ListVersionsAsync(req.WorkflowId, ct);
        await Send.OkAsync(versions, ct);
    }
}
