using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface IFinancialLifeGraphRetrievalService
{
    Task<GraphRetrievalResult<BillPaymentHistoryResponse>> GetBillPaymentHistoryAsync(
        string nodeKey,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    Task<GraphRetrievalResult<GoalContributionHistoryResponse>> GetGoalContributionHistoryAsync(
        string nodeKey,
        CancellationToken cancellationToken = default);

    Task<GraphRetrievalResult<AccountStatementResponse>> GetAccountStatementAsync(
        string nodeKey,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<GraphRetrievalResult<PartyObligationSummaryResponse>> GetPartyObligationSummaryAsync(
        string nodeKey,
        CancellationToken cancellationToken = default);
}
