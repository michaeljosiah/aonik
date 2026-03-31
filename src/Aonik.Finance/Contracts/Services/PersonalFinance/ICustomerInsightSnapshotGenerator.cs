using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface ICustomerInsightSnapshotGenerator
{
    Task<GeneratedCustomerInsightSnapshot> GenerateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
