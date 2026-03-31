using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface ICustomerInsightSnapshotService
{
    Task<CustomerInsightSnapshotResponse> GenerateCurrentSnapshotAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
