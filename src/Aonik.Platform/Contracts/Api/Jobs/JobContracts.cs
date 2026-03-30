namespace Aonik.Platform.Contracts.Api.Jobs;

// ── Scheduled Jobs ──────────────────────────────────────────────────

public record ScheduledJobSummary(
    string JobName,
    string GroupName,
    string? Description,
    string? CronExpression,
    string Status,
    DateTime? NextFireTimeUtc,
    DateTime? PreviousFireTimeUtc);

public record ScheduledJobListResponse(
    IReadOnlyList<ScheduledJobSummary> Jobs);

public record ScheduledJobActionResponse(
    string JobName,
    string Action,
    bool Success,
    string? Message);
