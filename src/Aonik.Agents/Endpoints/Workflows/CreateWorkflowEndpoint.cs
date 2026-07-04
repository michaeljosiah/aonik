using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class CreateWorkflowEndpoint
    : Endpoint<WorkflowSaveRequest, WorkflowGraphResponse>
{
    private readonly IWorkflowService _service;

    public CreateWorkflowEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Post("/ai/workflows");
        Policies("AdminWritePolicy");
        Summary(s =>
        {
            s.Summary = "Create a workflow";
            s.Description = "Creates a new workflow from the editor's full graph payload. Returns the canonical graph (with server-assigned Guids) so the editor can rehydrate.";
            s.Response(200, "Created");
            s.Response(400, "Invalid graph (e.g. missing trigger, dangling edge endpoint)");
            s.Response(409, "Slug already exists for this tenant");
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(WorkflowSaveRequest req, CancellationToken ct)
    {
        try
        {
            var graph = await _service.SaveAsync(req, ct);
            await Send.OkAsync(graph, ct);
        }
        catch (ArgumentException ex)
        {
            await Send.ResultAsync(Microsoft.AspNetCore.Http.Results.BadRequest(new { error = ex.Message }));
        }
    }
}
