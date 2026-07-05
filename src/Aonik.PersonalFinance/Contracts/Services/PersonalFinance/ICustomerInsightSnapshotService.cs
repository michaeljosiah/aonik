using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface ICustomerInsightSnapshotService
{
    Task<CustomerInsightSnapshotResponse> GenerateCurrentSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
