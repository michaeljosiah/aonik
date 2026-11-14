using System.Text.Json;

using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Agents.Services.Workflows;

internal sealed class WorkflowContinuationRequestedHandler(ITaskService tasks, ITenantProvider tenantProvider)
    : IEventHandler<WorkflowContinuationRequestedEvent>
{
    public Task HandleAsync(WorkflowContinuationRequestedEvent message, CancellationToken cancellationToken = default)
    {
        if (tenantProvider.GetCurrentTenantId() != message.TenantId || message.EventId == Guid.Empty)
            throw new InvalidOperationException("Workflow continuation event scope is invalid.");
        return tasks.ScheduleAsync(new($"Continue workflow run {message.RunId:D}", TaskKinds.ScheduledAction,
            "workflow_continue", JsonSerializer.Serialize(new WorkflowContinuationPayload(message.RunId, message.Continuation)),
            TaskAssigneeTypes.System, SubjectType: "WorkflowRun", SubjectId: message.RunId,
            RunAtUtc: DateTime.SpecifyKind(message.NotBeforeUtc, DateTimeKind.Utc), MaxRuns: 1,
            CorrelationId: $"workflow:{message.RunId:D}", SourceModule: "Agents", TaskId: message.EventId), cancellationToken);
    }
}

internal sealed record WorkflowContinuationPayload(Guid RunId, int Continuation);
