using FastEndpoints;

using Aonik.Agents.Contracts.Models.Workflows;
using Aonik.Agents.Contracts.Services;

namespace Aonik.Agents.Endpoints.Workflows;

internal sealed class GetWorkflowRunEndpoint(IWorkflowRunService runs) : EndpointWithoutRequest<WorkflowRunDetail>
{
    public override void Configure() => Get("/ai/workflow-runs/{RunId:guid}");

    public override async Task HandleAsync(CancellationToken ct)
    {
        WorkflowRunDetail? result = await runs.GetAsync(Route<Guid>("RunId"), ct);
        if (result is null) await Send.NotFoundAsync(ct);
        else await Send.OkAsync(result, ct);
    }
}
