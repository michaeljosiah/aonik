using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class UpdateWorkflowEndpoint
    : Endpoint<WorkflowSaveRequest, WorkflowGraphResponse>
{
    private readonly IWorkflowService _service;

    public UpdateWorkflowEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Put("/ai/workflows/{Slug}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Update a workflow's graph";
            s.Description = "Replaces the workflow's nodes and edges with the request payload, snapshotting the prior graph as a WorkflowVersion row. Auto-bumps the patch version tag.";
            s.Response(200, "Updated");
            s.Response(400, "Invalid graph");
            s.Response(404, "Workflow not found");
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(WorkflowSaveRequest req, CancellationToken ct)
    {
        // The PUT route's {Slug} segment already carries the slug, but the
        // payload also includes one — they must agree. Trust the path
        // (consistent with REST conventions) and rewrite the request slug
        // if the body disagrees.
        var routeSlug = Route<string>("Slug");
        var requestToUse = !string.IsNullOrWhiteSpace(routeSlug) && !string.Equals(routeSlug, req.Slug, StringComparison.Ordinal)
            ? req with { Slug = routeSlug! }
            : req;

        try
        {
            var graph = await _service.SaveAsync(requestToUse, ct);
            await Send.OkAsync(graph, ct);
        }
        catch (ArgumentException ex)
        {
            await Send.ResultAsync(Microsoft.AspNetCore.Http.Results.BadRequest(new { error = ex.Message }));
        }
    }
}
