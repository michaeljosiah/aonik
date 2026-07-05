using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface ICustomerInsightSnapshotGenerator
{
    Task<GeneratedCustomerInsightSnapshot> GenerateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
