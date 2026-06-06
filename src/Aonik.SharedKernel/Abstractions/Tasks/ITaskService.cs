namespace Aonik.SharedKernel.Abstractions.Tasks;

/// <summary>
/// The single cross-module contract for scheduling and managing data-defined
/// units of future work — "do this thing, about this subject, at this time (or
/// on this cadence)" (Spec 034). Implemented by <c>WorkItemService</c> in the
/// Platform module and consumed by any module through SharedKernel without a
/// reference to Platform's task entities (mirrors the no-cross-module-reference
/// pattern of ADR-006). Callers never touch Quartz.
/// </summary>
public interface ITaskService
{
    /// <summary>Schedules a new task. The <see cref="ScheduleTaskRequest.ActionType"/> must match a registered <see cref="ITaskActionHandler"/>.</summary>
    Task<TaskResponse> ScheduleAsync(ScheduleTaskRequest request, CancellationToken ct = default);

    /// <summary>Gets a single task by id, or null if it does not exist in the current tenant.</summary>
    Task<TaskResponse?> GetAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Lists tasks about a given subject (e.g. all reminders for a bill).</summary>
    Task<IReadOnlyList<TaskResponse>> ListForSubjectAsync(string subjectType, Guid subjectId, CancellationToken ct = default);

    /// <summary>Lists tasks for a given assignee (a user, an agent descriptor, or System).</summary>
    Task<IReadOnlyList<TaskResponse>> ListForAssigneeAsync(string assigneeType, Guid? assigneeId, CancellationToken ct = default);

    /// <summary>Pauses a scheduled task so the dispatcher skips it until resumed.</summary>
    Task PauseAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Resumes a paused task, re-arming it for its next occurrence.</summary>
    Task ResumeAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Cancels a task permanently; it will never fire again.</summary>
    Task CancelAsync(Guid taskId, CancellationToken ct = default);
}

/// <summary>
/// A request to schedule a task. Provide either <see cref="RunAtUtc"/> (one-off)
/// or <see cref="RecurrenceCron"/> (recurring). The product/API vocabulary is
/// "task"; the persisted CLR entity is named <c>WorkItem</c> to avoid colliding
/// with <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public sealed record ScheduleTaskRequest(
    string Title,
    string Kind,                       // Reminder | ScheduledAction | AgentAssignment
    string ActionType,                 // must match a registered ITaskActionHandler
    string ActionPayloadJson,
    string AssigneeType,               // System | User | Agent
    Guid? AssigneeId = null,
    string? AssigneeKey = null,        // agent descriptor name when Agent
    string? SubjectType = null,
    Guid? SubjectId = null,
    DateTime? RunAtUtc = null,         // OneOff
    string? RecurrenceCron = null,     // Recurring (Quartz cron expression)
    string? Timezone = null,           // IANA id for cron evaluation
    DateTime? StartAtUtc = null,
    DateTime? EndAtUtc = null,
    int? MaxRuns = null,
    string? Description = null,
    int Priority = 0,
    string? CorrelationId = null,
    string? SourceModule = null);      // origin module, e.g. "PersonalFinance"

/// <summary>A task as returned to callers and the admin UI.</summary>
public sealed record TaskResponse(
    Guid Id,
    Guid TenantId,
    string Title,
    string? Description,
    string Kind,
    string? SubjectType,
    Guid? SubjectId,
    string AssigneeType,
    Guid? AssigneeId,
    string? AssigneeKey,
    string ActionType,
    string ScheduleType,
    DateTime? NextRunAtUtc,
    string? RecurrenceCron,
    string? Timezone,
    DateTime? StartAtUtc,
    DateTime? EndAtUtc,
    int RunCount,
    int? MaxRuns,
    string Status,
    int Priority,
    string SourceModule,
    string? CorrelationId,
    string? LastError,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
