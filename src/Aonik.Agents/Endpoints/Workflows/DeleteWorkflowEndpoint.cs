using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record DeleteWorkflowRequest(string Slug, string? ExpectedVersion);

internal sealed class DeleteWorkflowEndpoint
    : Endpoint<DeleteWorkflowRequest>
{
    private readonly IWorkflowService _service;

    public DeleteWorkflowEndpoint(IWorkflowService service)
    {
        _service = service;
    }

    public override void Configure()
    {
        Delete("/ai/workflows/{Slug}");
        Summary(s =>
        {
            s.Summary = "Delete a workflow";
            s.Description = "Soft-deletes the workflow and its graph. Run history and version snapshots are preserved.";
            s.Response(204, "Deleted");
            s.Response(404, "Workflow not found");
        });
        Options(x => x.WithTags("AI Workflows"));
    }

    public override async Task HandleAsync(DeleteWorkflowRequest req, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteAsync(Route<string>("Slug")!, req.ExpectedVersion, ct);
            if (!deleted)
            {
                await Send.NotFoundAsync(ct);
                return;
            }
            await Send.NoContentAsync(ct);
        }
        catch (ArgumentException ex)
        {
            await Send.ResultAsync(Results.BadRequest(new { error = ex.Message }));
        }
        catch (WorkflowConflictException ex)
        {
            await Send.ResultAsync(Results.Conflict(new { error = ex.Message }));
        }
    }
}
