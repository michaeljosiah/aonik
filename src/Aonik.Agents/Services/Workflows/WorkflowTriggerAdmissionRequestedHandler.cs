using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowTriggerAdmissionRequestedHandler(ITaskService tasks, ITenantProvider tenantProvider, WorkflowTriggerTaskProtector proofs)
    : IEventHandler<WorkflowTriggerAdmissionRequestedEvent>
{
    public Task HandleAsync(WorkflowTriggerAdmissionRequestedEvent message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId != tenantProvider.GetCurrentTenantId() || message.EventId == Guid.Empty || message.SourceEventId == Guid.Empty
            || message.BindingId == Guid.Empty || message.Revision < 1 || message.SubjectType != "Invoice" || message.SubjectId == Guid.Empty
            || message.SourceOccurredAtUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("Invalid trigger admission event scope.");
        Guid taskId = message.EventId;
        var payload = new WorkflowTriggerAdmissionPayload(message.BindingId, message.Revision, message.SourceEventId, message.SourceOccurredAtUtc,
            message.SubjectType, message.SubjectId, message.CausationRunId, message.SelfGenerated);
        if (proofs.ReadAdmission(message.TaskProof, message.TenantId, taskId) != payload) throw new ArgumentException("The trigger admission does not match its trusted source proof.");
        return tasks.ScheduleAsync(new($"Admit workflow trigger {message.BindingId:D}", TaskKinds.ScheduledAction, "workflow_trigger_admit",
            message.TaskProof, TaskAssigneeTypes.System, SubjectType: "WorkflowTriggerBinding", SubjectId: message.BindingId,
            RunAtUtc: message.SourceOccurredAtUtc, MaxRuns: 1, SourceModule: "Agents", CorrelationId: $"trigger:{message.SourceEventId:D}", TaskId: taskId), cancellationToken);
    }
}
