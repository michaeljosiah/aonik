namespace Aonik.Cli.Models;

// ── Commitment lifecycle API contracts (Spec 044) ───────────────────
// CommitmentDetail is intentionally a partial projection of the API record —
// System.Text.Json populates the fields we display and ignores the rest.

public sealed record CommitmentDetail(
    Guid CommitmentId,
    string CommitmentType,
    string CommitmentKind,
    string DisplayName,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    string Status,
    string? RhythmLabel,
    Guid? CareEntityId);

public sealed record CommitmentCycleResponse(
    Guid Id,
    Guid CommitmentId,
    DateTime DueDate,
    string Status,
    Guid? PaymentLogId,
    string? SkipReason,
    DateTime? SnoozedUntil,
    DateTime? ResolvedAt,
    DateTime CreatedAt);

public sealed record CreateSupportCommitmentRequest(
    Guid CareEntityId,
    string DisplayName,
    decimal? ExpectedAmount,
    string Currency,
    string RhythmUnit,
    int RhythmInterval,
    int? AnchorDay,
    IReadOnlyList<DateTime>? TermDates,
    DateTime FirstDueDate,
    int? ReminderDaysBefore,
    Guid? PaidFromAccountId,
    string? Notes);

public sealed record MarkCommitmentDoneRequest(
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime? Date,
    string Channel,
    string? Note,
    Guid? IdempotencyKey);

// ── Command options ─────────────────────────────────────────────────

public sealed record CreateSupportCommitmentOptions(
    Guid CareEntityId,
    string DisplayName,
    decimal? ExpectedAmount,
    string Currency,
    string RhythmUnit,
    int RhythmInterval,
    int? AnchorDay,
    DateTime FirstDueDate,
    int? ReminderDaysBefore,
    string? Notes,
    OutputMode OutputMode);

public sealed record MarkCommitmentDoneOptions(
    Guid CommitmentId,
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime? Date,
    string Channel,
    string? Note,
    Guid? IdempotencyKey,
    OutputMode OutputMode);
