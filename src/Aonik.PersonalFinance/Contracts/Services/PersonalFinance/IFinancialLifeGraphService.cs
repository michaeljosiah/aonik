using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface IFinancialLifeGraphService
{
    Task<FinancialLifeGraphResponse> GetGraphAsync(CancellationToken cancellationToken = default);

    Task<FinancialLifeGraphSummaryResponse> GetGraphSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpcomingObligationResponse>> GetUpcomingObligationsAsync(
        int withinDays = 30,
        CancellationToken cancellationToken = default);

    Task<HouseholdFinanceContextResponse> GetHouseholdFinanceContextAsync(CancellationToken cancellationToken = default);

    Task<RelatedPartyFinanceContextResponse> GetRelatedPartyFinanceContextAsync(CancellationToken cancellationToken = default);
}
