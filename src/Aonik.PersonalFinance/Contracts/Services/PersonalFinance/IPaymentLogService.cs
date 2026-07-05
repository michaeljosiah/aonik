using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

/// <summary>
/// Customer-scoped CRUD over <c>PaymentLog</c> (Spec 045): idempotent create,
/// soft-delete + restore, and the confirm-gated corroboration link. Also the
/// source of the per-currency profile totals + recent acts (Spec 043 §8).
/// Every operation is isolated to the current tenant + user.
/// </summary>
public interface IPaymentLogService
{
    /// <summary>
    /// Creates a log. Idempotent on <c>IdempotencyKey</c> — a replay returns the
    /// existing log. Validates that CareEntityId (and CommitmentId, when present)
    /// belong to the caller. Also the entry point Spec 044 mark-done calls.
    /// </summary>
    Task<PaymentLogResponse> CreateAsync(
        CreatePaymentLogRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentLogListResponse> ListAsync(
        Guid? careEntityId,
        Guid? commitmentId,
        int? year,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PaymentLogResponse?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns <c>null</c> when the log is not owned by the current user.</summary>
    Task<PaymentLogResponse?> UpdateAsync(
        Guid id,
        UpdatePaymentLogRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Soft-delete (30-day restore window). Returns <c>false</c> when not owned.</summary>
    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Restores a soft-deleted log within the 30-day window. Returns <c>null</c> if not restorable.</summary>
    Task<PaymentLogResponse?> RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Confirm-gate a corroboration link (CorroborationStatus → confirmed). Returns <c>null</c> if not owned.</summary>
    Task<PaymentLogResponse?> LinkTransactionAsync(
        Guid id,
        Guid transactionId,
        CancellationToken cancellationToken = default);

    /// <summary>Remove a corroboration link (CorroborationStatus → none). Returns <c>null</c> if not owned.</summary>
    Task<PaymentLogResponse?> UnlinkTransactionAsync(Guid id, CancellationToken cancellationToken = default);

    // ── Profile helpers (Spec 043 §8) — degrade-free reads ──────────────

    /// <summary>Per-currency totals for one entity (year filter optional) — never converted.</summary>
    Task<IReadOnlyList<CurrencyTotal>> GetEntityYearTotalsAsync(
        Guid careEntityId,
        int? year,
        CancellationToken cancellationToken = default);

    /// <summary>The most recent acts for one entity, newest first.</summary>
    Task<IReadOnlyList<CareEntityPaymentLogSummary>> GetRecentForEntityAsync(
        Guid careEntityId,
        int count,
        CancellationToken cancellationToken = default);
}
