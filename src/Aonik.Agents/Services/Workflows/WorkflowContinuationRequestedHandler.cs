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
        return tasks.ScheduleAsync(new(message.Purpose ?? $"Continue workflow run {message.RunId:D}", TaskKinds.ScheduledAction,
            "workflow_continue", JsonSerializer.Serialize(new WorkflowContinuationPayload(message.RunId, message.Continuation)),
            message.AssignedUserId.HasValue ? TaskAssigneeTypes.User : TaskAssigneeTypes.System, message.AssignedUserId,
            SubjectType: "WorkflowRun", SubjectId: message.RunId,
            RunAtUtc: DateTime.SpecifyKind(message.NotBeforeUtc, DateTimeKind.Utc), MaxRuns: 1,
            Description: message.Purpose is null ? null : $"Review this task in /ai/workflow-runs/{message.RunId:D}. Reviewer role: {message.ReviewRoleId?.ToString("D") ?? "named user"}. The due action checks timeout; it cannot supply a human response.",
            CorrelationId: $"workflow:{message.RunId:D}", SourceModule: "Agents", TaskId: message.EventId), cancellationToken);
    }
}

internal sealed record WorkflowContinuationPayload(Guid RunId, int Continuation);
