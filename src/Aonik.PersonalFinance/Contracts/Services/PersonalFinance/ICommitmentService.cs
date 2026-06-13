using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

/// <summary>
/// Unified read-side service for personal-finance recurring commitments.
/// Projects bills, subscriptions, and debt repayments into a single
/// <see cref="CommitmentItem"/>/<see cref="CommitmentDetail"/> read model.
/// </summary>
public interface ICommitmentService
{
    /// <summary>
    /// Lists commitments across all three source types with optional filtering and pagination.
    /// </summary>
    Task<CommitmentListResponse> ListCommitmentsAsync(
        CommitmentListFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a single commitment by ID. Resolves the underlying source type.
    /// </summary>
    Task<CommitmentDetail?> GetCommitmentAsync(
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a commitment from an existing personal transaction.
    /// Persists the correct underlying entity based on <c>CommitmentType</c>
    /// and links it to the source transaction.
    /// </summary>
    Task<CommitmentDetail> CreateFromTransactionAsync(
        CreateCommitmentFromTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a detected commitment (transitions VerificationStatus from Detected → Confirmed).
    /// </summary>
    Task<CommitmentDetail> ConfirmDetectedAsync(
        Guid commitmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a detected commitment (transitions VerificationStatus from Detected → Rejected).
    /// </summary>
    Task RejectDetectedAsync(
        Guid commitmentId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists only detected (unreviewed) commitments awaiting user confirmation.
    /// </summary>
    Task<IReadOnlyList<CommitmentItem>> ListDetectedAsync(
        CancellationToken cancellationToken = default);

    // ── Support commitment lifecycle (Spec 044) ─────────────────────────

    /// <summary>
    /// Authors a Support commitment attached to a CareEntity with a structured
    /// rhythm — the first manual-create path for a commitment-projected entity.
    /// Opens the first cycle and arms a reminder.
    /// </summary>
    Task<CommitmentDetail> CreateSupportAsync(
        CreateSupportCommitmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Edits a commitment (never rewrites past cycles). Null if not owned.</summary>
    Task<CommitmentDetail?> UpdateSupportAsync(
        Guid commitmentId,
        UpdateSupportCommitmentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the current cycle done: writes a PaymentLog (Spec 045), records the
    /// cycle Paid, rolls the due date forward, opens the next cycle, re-arms the
    /// reminder. Idempotent per cycle. Null if not owned.
    /// </summary>
    Task<CommitmentDetail?> MarkDoneAsync(
        Guid commitmentId,
        MarkCommitmentDoneRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Skips the current cycle (honest history), advances, re-arms. Null if not owned.</summary>
    Task<CommitmentDetail?> SkipCycleAsync(
        Guid commitmentId,
        string? reason,
        CancellationToken cancellationToken = default);

    /// <summary>Reschedules the current cycle's reminder without resolving it. Null if not owned.</summary>
    Task<CommitmentDetail?> SnoozeAsync(
        Guid commitmentId,
        DateTime until,
        CancellationToken cancellationToken = default);

    /// <summary>Pauses reminders. Null if not owned.</summary>
    Task<CommitmentDetail?> PauseAsync(Guid commitmentId, CancellationToken cancellationToken = default);

    /// <summary>Resumes and re-arms from today. Null if not owned.</summary>
    Task<CommitmentDetail?> ResumeAsync(Guid commitmentId, CancellationToken cancellationToken = default);

    /// <summary>Per-cycle history timeline (paged, newest first). Null if the commitment is not owned.</summary>
    Task<IReadOnlyList<CommitmentCycleResponse>?> GetCyclesAsync(
        Guid commitmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Idempotent backfill: opens one cycle per active commitment that has none,
    /// so existing detected/promoted commitments enter the lifecycle. Returns the
    /// number of cycles opened.
    /// </summary>
    Task<int> BackfillOpenCyclesAsync(CancellationToken cancellationToken = default);
}
