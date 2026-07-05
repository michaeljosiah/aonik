using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface ICustomerInsightSnapshotReader
{
    Task<CustomerInsightSnapshotResponse?> GetCurrentSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CustomerInsightSnapshotResponse?> GetSnapshotAsync(
        Guid snapshotId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CustomerInsightSnapshotHistoryItemResponse>> GetSnapshotHistoryAsync(
        Guid userId,
        int take = 20,
        CancellationToken cancellationToken = default);
}
