using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IPersonalFinanceNarrativeInsightsService
{
    Task<PersonalSpendingNarrativeInsightResponse> GenerateSpendingNarrativeAsync(
        GeneratePersonalSpendingNarrativeRequest request,
        CancellationToken cancellationToken = default);
}
