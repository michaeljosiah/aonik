using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class CancelWorkflowRunEndpoint(IWorkflowRunService runs) : Endpoint<WorkflowRunHttpCommand, WorkflowRunDetail>
{
    public override void Configure() => Post("/ai/workflow-runs/{RunId:guid}/cancel");

    public override async Task HandleAsync(WorkflowRunHttpCommand request, CancellationToken ct)
    {
        try { await Send.OkAsync(await runs.CancelAsync(Route<Guid>("RunId"), new(request.ExpectedVersion), ct), ct); }
        catch (KeyNotFoundException) { await Send.NotFoundAsync(ct); }
        catch (WorkflowConflictException ex) { await Send.ResultAsync(Results.Conflict(new { error = ex.Message })); }
    }
}
