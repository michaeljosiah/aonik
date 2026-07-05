using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface IPersonalFinanceNarrativeInsightsService
{
    Task<PersonalSpendingNarrativeInsightResponse> GenerateSpendingNarrativeAsync(
        GeneratePersonalSpendingNarrativeRequest request,
        CancellationToken cancellationToken = default);
}
