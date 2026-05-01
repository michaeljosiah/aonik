using Aonik.Agents.Contracts.Services;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed record DeleteWorkflowRequest(string Slug);

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
        Policies("AdminUserPolicy");
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
        var deleted = await _service.DeleteAsync(req.Slug, ct);
        if (!deleted)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.NoContentAsync(ct);
    }
}
