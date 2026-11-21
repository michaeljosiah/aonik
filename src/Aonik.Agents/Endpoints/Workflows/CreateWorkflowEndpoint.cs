using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;
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
        Summary(s =>
        {
            s.Summary = "Create a workflow";
            s.Description = "Creates a new workflow from the editor's full graph payload. Returns the canonical graph (with server-assigned Guids) so the editor can rehydrate.";
            s.Response(201, "Created");
            s.Response(400, "Invalid graph (e.g. missing trigger, dangling edge endpoint)");
            s.Response(409, "Slug already exists for this tenant");
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(WorkflowSaveRequest req, CancellationToken ct)
    {
        try
        {
            var graph = await _service.CreateAsync(req, ct);
            await Send.CreatedAtAsync<GetWorkflowBySlugEndpoint>(new { Slug = graph.Slug }, graph, cancellation: ct);
        }
        catch (ArgumentException ex)
        {
            await Send.ResultAsync(Microsoft.AspNetCore.Http.Results.BadRequest(new { error = ex.Message }));
        }
        catch (WorkflowConflictException ex)
        {
            await Send.ResultAsync(Results.Conflict(new { error = ex.Message }));
        }
    }
}
