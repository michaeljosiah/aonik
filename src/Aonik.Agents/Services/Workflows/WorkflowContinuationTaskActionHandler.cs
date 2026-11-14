using System.Text.Json;

using Aonik.Agents.Workflows.Graph;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowContinuationTaskActionHandler(IGraphWorkflowRunner runner, ITenantProvider tenantProvider) : ITaskActionHandler
{
    public string ActionType => "workflow_continue";

    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        WorkflowContinuationPayload payload = JsonSerializer.Deserialize<WorkflowContinuationPayload>(context.ActionPayloadJson, WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Workflow continuation payload is required.");
        if (context.TenantId != tenantProvider.GetCurrentTenantId() || context.AssigneeType != TaskAssigneeTypes.System
            || context.Kind != TaskKinds.ScheduledAction || context.SubjectType != "WorkflowRun" || context.SubjectId != payload.RunId
            || payload.RunId == Guid.Empty || payload.Continuation < 0)
            throw new ArgumentException("Workflow continuation scope is invalid.");
        GraphWorkflowContinuation result = await runner.ContinueAsync(payload.RunId, payload.Continuation, cancellationToken);
        return result.Status == "Busy" ? new(TaskActionOutcome.Failed, Error: "The workflow continuation is still leased.")
            : new(TaskActionOutcome.Succeeded, JsonSerializer.Serialize(result, WorkflowDefinitionCodec.JsonOptions));
    }
}
