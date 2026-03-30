namespace Aonik.Platform.Contracts.Api.Jobs;

// ── Scheduled Jobs ──────────────────────────────────────────────────

public record ScheduledJobSummary(
    string JobName,
    string GroupName,
    string? Description,
    string? CronExpression,
    string Status,
    DateTime? NextFireTimeUtc,
    DateTime? PreviousFireTimeUtc,
    string? DisplayName);

public record ScheduledJobListResponse(
    IReadOnlyList<ScheduledJobSummary> Jobs);

public record ScheduledJobActionResponse(
    string JobName,
    string Action,
    bool Success,
    string? Message,
    Guid? CommandId,
    string? CommandStatus);

// ── Job Detail ──────────────────────────────────────────────────────

public record ScheduledJobDetailResponse(
    string JobName,
    string GroupName,
    string DisplayName,
    string Description,
    string CronExpression,
    string TimeZoneId,
    string State,
    DateTime? NextFireTimeUtc,
    DateTime? PreviousFireTimeUtc,
    string? LastOutcome,
    string? LastOutcomeSummary,
    int? LastDurationMs,
    DateTime LastSyncedAtUtc);

// ── Run History ─────────────────────────────────────────────────────

public record ScheduledJobRunSummary(
    Guid Id,
    string Outcome,
    string? ErrorMessage,
    int DurationMs,
    string TriggeredBy,
    DateTime FiredAtUtc,
    DateTime CompletedAtUtc,
    string? FireInstanceId);

// ── Command History ─────────────────────────────────────────────────

public record ScheduledJobCommandSummary(
    Guid Id,
    string CommandType,
    string Status,
    string? ResultMessage,
    Guid? RequestedByUserId,
    DateTime CreatedAt,
    DateTime? ProcessedAtUtc);

// ── Scheduler Health ────────────────────────────────────────────────

public record SchedulerHealthResponse(
    string SchedulerName,
    string SchedulerInstanceId,
    bool IsStarted,
    bool InStandbyMode,
    int ThreadPoolSize,
    int ActiveJobCount,
    int TotalJobCount,
    int TotalTriggerCount,
    DateTime RecordedAtUtc);
