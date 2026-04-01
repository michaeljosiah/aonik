namespace Aonik.SharedKernel.Abstractions.Ai;

public interface ICustomerInsightAiSummaryReader
{
    Task<CustomerInsightAiSummaryResponse?> GetCurrentSummaryForSnapshotAsync(
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default);

    Task<CustomerInsightAiSummaryResponse?> GetSummaryAsync(
        Guid summaryId,
        CancellationToken cancellationToken = default);
}
