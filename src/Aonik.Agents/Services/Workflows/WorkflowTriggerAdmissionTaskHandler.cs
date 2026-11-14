using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerAdmissionTaskHandler(ITenantProvider tenantProvider, WorkflowTriggerTaskProtector proofs,
    WorkflowTriggerAdmissionService admissions) : ITaskActionHandler
{
    public string ActionType => "workflow_trigger_admit";
    public Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
    {
        if (context.TenantId != tenantProvider.GetCurrentTenantId() || context.Kind != TaskKinds.ScheduledAction || context.AssigneeType != TaskAssigneeTypes.System)
            throw new ArgumentException("Invalid automatic admission task scope.");
        WorkflowTriggerAdmissionPayload payload = proofs.ReadAdmission(context.ActionPayloadJson, context.TenantId, context.WorkItemId);
        if (context.SubjectType != "WorkflowTriggerBinding" || context.SubjectId != payload.BindingId || context.ScheduledForUtc != payload.SourceOccurredAtUtc)
            throw new ArgumentException("The automatic admission task does not match its protected source occurrence.");
        return admissions.AdmitAsync(payload, cancellationToken);
    }
}
