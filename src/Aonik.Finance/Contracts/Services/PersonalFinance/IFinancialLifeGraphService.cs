using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IFinancialLifeGraphService
{
    Task<FinancialLifeGraphResponse> GetGraphAsync(CancellationToken cancellationToken = default);

    Task<FinancialLifeGraphSummaryResponse> GetGraphSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UpcomingObligationResponse>> GetUpcomingObligationsAsync(
        int withinDays = 30,
        CancellationToken cancellationToken = default);
}
