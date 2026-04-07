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
}
