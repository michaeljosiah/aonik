using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Agents.Workflows.Graph;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowContinuationTaskActionHandler(IGraphWorkflowRunner runner, ITenantProvider tenantProvider, IClock? clock = null,
    AgentsDbContext? database = null) : ITaskActionHandler
{
    public string ActionType => "workflow_continue";

    public async Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        WorkflowContinuationPayload payload = JsonSerializer.Deserialize<WorkflowContinuationPayload>(context.ActionPayloadJson, WorkflowDefinitionCodec.JsonOptions)
            ?? throw new ArgumentException("Workflow continuation payload is required.");
        bool assignedHumanExpiry = context.AssigneeType == TaskAssigneeTypes.User && database is not null
            && context.AssigneeId.HasValue && await database.ToolApprovalRequests.AsNoTracking().AnyAsync(request => request.TenantId == context.TenantId
                && request.Id == context.WorkItemId && request.ToolName == WorkflowHumanTaskContract.ToolName
                && database.WorkflowStepRuns.Any(step => step.Id == request.WorkflowStepRunId && step.RunId == payload.RunId
                    && step.TenantId == context.TenantId), cancellationToken);
        if (context.TenantId != tenantProvider.GetCurrentTenantId() || (context.AssigneeType != TaskAssigneeTypes.System && !assignedHumanExpiry)
            || context.Kind != TaskKinds.ScheduledAction || context.SubjectType != "WorkflowRun" || context.SubjectId != payload.RunId
            || payload.RunId == Guid.Empty || payload.Continuation < 0)
            throw new ArgumentException("Workflow continuation scope is invalid.");
        GraphWorkflowContinuation result = await runner.ContinueAsync(payload.RunId, payload.Continuation, cancellationToken);
        return result.Status == "Busy" ? new(TaskActionOutcome.Deferred, Error: "The workflow continuation is still leased.", DeferredUntilUtc: (clock?.UtcNow ?? DateTime.UtcNow).AddSeconds(10))
            : new(TaskActionOutcome.Succeeded, JsonSerializer.Serialize(result, WorkflowDefinitionCodec.JsonOptions));
    }
}
