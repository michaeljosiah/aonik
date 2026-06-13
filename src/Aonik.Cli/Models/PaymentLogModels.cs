namespace Aonik.Cli.Models;

// ── PaymentLog API contracts (Spec 045) ─────────────────────────────
// CurrencyTotal is defined in CareEntityModels.cs and reused here.

public sealed record CreatePaymentLogRequest(
    Guid CareEntityId,
    Guid? CommitmentId,
    Guid? CommitmentCycleId,
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime Date,
    string Channel,
    string Origin,
    string? Note,
    Guid? IdempotencyKey);

public sealed record UpdatePaymentLogRequest(
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime Date,
    string Channel,
    string? Note);

public sealed record PaymentLogResponse(
    Guid Id,
    Guid CareEntityId,
    Guid? CommitmentId,
    Guid? CommitmentCycleId,
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime Date,
    string Channel,
    string Origin,
    string? Note,
    Guid? SourceTransactionId,
    string CorroborationStatus,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PaymentLogListResponse(
    IReadOnlyList<PaymentLogResponse> Items,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record YearSummary(
    int Year,
    IReadOnlyList<CurrencyTotal> Totals,
    int EntityCount);

// ── Command options ─────────────────────────────────────────────────

public sealed record CreatePaymentLogOptions(
    Guid CareEntityId,
    Guid? CommitmentId,
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime? Date,
    string Channel,
    string Origin,
    string? Note,
    Guid? IdempotencyKey,
    OutputMode OutputMode);

public sealed record ListPaymentLogsOptions(
    Guid? CareEntityId,
    Guid? CommitmentId,
    int? Year,
    int Page,
    int PageSize,
    OutputMode OutputMode);

public sealed record UpdatePaymentLogOptions(
    Guid Id,
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime? Date,
    string Channel,
    string? Note,
    OutputMode OutputMode);
