using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services.Workflows;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class RequestWorkflowRetirementEndpoint(IWorkflowGovernanceService service)
    : Endpoint<WorkflowGovernanceCommand, WorkflowGovernanceRequestResult>
{
    public override void Configure() => Post("/ai/workflows/{Slug}/retirement-requests");

    public override async Task HandleAsync(WorkflowGovernanceCommand req, CancellationToken ct)
    {
        try
        {
            WorkflowGovernanceRequestResult result = await service.RequestAsync(req with { Slug = Route<string>("Slug")! }, true, ct);
            await Send.ResultAsync(Results.Json(result, statusCode: StatusCodes.Status202Accepted));
        }
        catch (KeyNotFoundException) { await Send.NotFoundAsync(ct); }
        catch (WorkflowConflictException ex) { await Send.ResultAsync(Results.Conflict(new { error = ex.Message })); }
        catch (ArgumentException ex) { await Send.ResultAsync(Results.BadRequest(new { error = ex.Message })); }
    }
}
