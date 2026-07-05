namespace Aonik.PersonalFinance.Contracts.Models;

// ── PaymentLog DTOs (Spec 045 §4/§9) ────────────────────────────────
// Note: CurrencyTotal and CareEntityPaymentLogSummary are defined in
// CareEntityModels.cs (Spec 043) and reused here.

public record CreatePaymentLogRequest(
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

public record UpdatePaymentLogRequest(
    decimal Amount,
    string Currency,
    decimal? ApproxGbp,
    DateTime Date,
    string Channel,
    string? Note);

public record PaymentLogResponse(
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

public record PaymentLogListResponse(
    IReadOnlyList<PaymentLogResponse> Items,
    int Page,
    int PageSize,
    bool HasMore);

/// <summary>Per-currency year summary for the Today hero (§7) — never a converted grand total.</summary>
public record YearSummary(
    int Year,
    IReadOnlyList<CurrencyTotal> Totals,
    int EntityCount);
