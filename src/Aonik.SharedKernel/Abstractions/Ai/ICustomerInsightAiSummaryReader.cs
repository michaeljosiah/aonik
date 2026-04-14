namespace Aonik.SharedKernel.Abstractions.Ai;

public interface ICustomerInsightAiSummaryReader
{
    Task<CustomerInsightAiSummaryResponse?> GetCurrentSummaryForSnapshotAsync(
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default);

    Task<CustomerInsightAiSummaryResponse?> GetSummaryAsync(
        Guid summaryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of the supplied snapshot ids that already have a non-superseded
    /// AI summary (Current or Failed). Used by the background job enumerator to skip
    /// snapshots that have already been processed and avoid runaway OpenAI spend.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetSnapshotIdsWithExistingSummariesAsync(
        IReadOnlyCollection<Guid> snapshotIds,
        CancellationToken cancellationToken = default);
}
