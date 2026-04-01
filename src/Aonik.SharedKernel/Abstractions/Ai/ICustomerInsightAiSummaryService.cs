namespace Aonik.SharedKernel.Abstractions.Ai;

public interface ICustomerInsightAiSummaryService
{
    Task<CustomerInsightAiSummaryResponse> GenerateCurrentSummaryAsync(
        Guid customerInsightSnapshotId,
        CancellationToken cancellationToken = default);
}
