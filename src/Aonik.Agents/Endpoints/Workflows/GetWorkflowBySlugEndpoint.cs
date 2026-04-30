using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record GetWorkflowBySlugRequest(string Slug);

internal sealed class GetWorkflowBySlugEndpoint
    : Endpoint<GetWorkflowBySlugRequest, WorkflowGraphResponse>
{
    private readonly IWorkflowService _service;

    public GetWorkflowBySlugEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Get("/ai/workflows/{Slug}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get workflow graph by slug";
            s.Description = "Returns the full graph (nodes + edges + comments) for the editor canvas.";
            s.Response(404, "Workflow not found");
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(GetWorkflowBySlugRequest req, CancellationToken ct)
    {
        var graph = await _service.GetBySlugAsync(req.Slug, ct);
        if (graph is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(graph, ct);
    }
}
