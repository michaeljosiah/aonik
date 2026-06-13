namespace Aonik.Finance.Contracts.Models.PersonalFinance;

// ── Commitment read model (unified projection over Bill, Subscription, DebtRepayment) ──

public record CommitmentItem(
    Guid CommitmentId,
    string CommitmentType,
    string VerificationStatus,
    string Origin,
    string DisplayName,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    string Frequency,
    string Status,
    bool Autopay,
    Guid? PaidFromAccountId,
    string? Category,
    decimal? ConfidenceScore,
    DateTime? LastPaidAt,
    decimal? LastPaidAmount,
    DateTime CreatedAt);

public record CommitmentDetail(
    Guid CommitmentId,
    string CommitmentType,
    string VerificationStatus,
    string Origin,
    string DisplayName,
    string? NormalizedMerchantOrPayee,
    decimal? Amount,
    string Currency,
    DateTime DueDate,
    string Frequency,
    string Status,
    bool Autopay,
    Guid? PaidFromAccountId,
    string? Category,
    string? SubCategory,
    decimal? ConfidenceScore,
    string? DetectionSource,
    Guid? SourceTransactionId,
    DateTime? LastObservedAt,
    DateTime? LastPaidAt,
    decimal? LastPaidAmount,
    string? Notes,
    string? AccountReference,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // ── Support commitment additions (Spec 044) ──
    Guid? CareEntityId = null,
    string CommitmentKind = "Bill",
    string? RhythmLabel = null);

public record CommitmentListResponse(
    IReadOnlyList<CommitmentItem> Items,
    int Page,
    int PageSize,
    bool HasMore,
    CommitmentTotals Totals);

public record CommitmentTotals(
    decimal TotalUpcomingAmount,
    int DueSoonCount,
    int DetectedCount,
    int BillsCount,
    int SubscriptionsCount,
    int DebtRepaymentsCount);

// ── Create from transaction ────────────────────────────────────

public record CreateCommitmentFromTransactionRequest(
    Guid TransactionId,
    string CommitmentType,
    string DisplayName,
    string Frequency,
    DateTime NextDueDate,
    decimal? ExpectedAmount,
    string Currency,
    Guid? PaidFromAccountId,
    bool Autopay,
    string? Notes,
    string? DebtType,
    string? AccountReference);

// ── Filter parameters ──────────────────────────────────────────

public record CommitmentListFilter(
    string? Type = null,
    string? VerificationStatus = null,
    string? Status = null,
    DateTime? DueFrom = null,
    DateTime? DueTo = null,
    Guid? AccountId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

// ── Support commitment lifecycle (Spec 044) ─────────────────────────

public record CreateSupportCommitmentRequest(
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

public record UpdateSupportCommitmentRequest(
    string DisplayName,
    decimal? ExpectedAmount,
    string Currency,
    string RhythmUnit,
    int RhythmInterval,
    int? AnchorDay,
    IReadOnlyList<DateTime>? TermDates,
    int? ReminderDaysBefore,
    string? Notes);

/// <summary>Body for mark-done — the payment-log draft (Spec 045) for this cycle.</summary>
public record MarkCommitmentDoneRequest(
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime? Date,
    string Channel,
    string? Note,
    Guid? IdempotencyKey);

public record SkipCommitmentRequest(string? Reason);

public record SnoozeCommitmentRequest(DateTime Until);

public record CommitmentCycleResponse(
    Guid Id,
    Guid CommitmentId,
    DateTime DueDate,
    string Status,
    Guid? PaymentLogId,
    string? SkipReason,
    DateTime? SnoozedUntil,
    DateTime? ResolvedAt,
    DateTime CreatedAt);
