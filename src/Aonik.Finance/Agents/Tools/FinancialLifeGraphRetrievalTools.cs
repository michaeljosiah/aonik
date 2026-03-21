using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Finance.Agents.Tools;

internal sealed class FinancialLifeGraphRetrievalTools
{
    private readonly IFinancialLifeGraphRetrievalService _retrievalService;

    private FinancialLifeGraphRetrievalTools(IFinancialLifeGraphRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    [Description("Retrieves the payment history for a specific bill over a bounded time window. Use this AFTER traversal has identified a bill of interest and you need to see actual payment records — how much was paid, when, and through which method. The time window is enforced to a maximum of 730 days.")]
    public async Task<GraphRetrievalResult<BillPaymentHistoryResponse>> GetBillPaymentHistory(
        [Description("The bill node key from traversal (format: bill:guid)")] string nodeKey,
        [Description("Optional start date for the history window (default: 730 days ago)")] DateTime? from = null,
        [Description("Optional end date for the history window (default: now)")] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        return await _retrievalService.GetBillPaymentHistoryAsync(nodeKey, from, to, cancellationToken);
    }

    [Description("Retrieves the contribution history for a specific savings goal. Use this AFTER traversal has identified a goal of interest and you need to see actual contributions — amounts, dates, and descriptions. Shows progress towards the goal target.")]
    public async Task<GraphRetrievalResult<GoalContributionHistoryResponse>> GetGoalContributionHistory(
        [Description("The goal node key from traversal (format: goal:guid)")] string nodeKey,
        CancellationToken cancellationToken = default)
    {
        return await _retrievalService.GetGoalContributionHistoryAsync(nodeKey, cancellationToken);
    }

    [Description("Retrieves a bounded account statement — all transactions for a specific account within a date range. Use this AFTER traversal has identified an account of interest and you need a complete transaction listing with running balance. The maximum window is 365 days.")]
    public async Task<GraphRetrievalResult<AccountStatementResponse>> GetAccountStatement(
        [Description("The account node key from traversal (format: personal-account:guid)")] string nodeKey,
        [Description("Start date for the statement (inclusive)")] DateTime from,
        [Description("End date for the statement (inclusive)")] DateTime to,
        CancellationToken cancellationToken = default)
    {
        return await _retrievalService.GetAccountStatementAsync(nodeKey, from, to, cancellationToken);
    }

    [Description("Retrieves an aggregated obligation summary for a specific related party — all recurring financial commitments (bills, subscriptions, remittance patterns) connected to that party. Use this AFTER traversal has identified a party of interest and you need to understand the total financial obligation to that party, including an estimated monthly total.")]
    public async Task<GraphRetrievalResult<PartyObligationSummaryResponse>> GetPartyObligationSummary(
        [Description("The party node key from traversal (format: party:guid)")] string nodeKey,
        CancellationToken cancellationToken = default)
    {
        return await _retrievalService.GetPartyObligationSummaryAsync(nodeKey, cancellationToken);
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new FinancialLifeGraphRetrievalTools(serviceProvider.GetRequiredService<IFinancialLifeGraphRetrievalService>());

        yield return AIFunctionFactory.Create(tools.GetBillPaymentHistory, name: "finance_graph_get_bill_payment_history");
        yield return AIFunctionFactory.Create(tools.GetGoalContributionHistory, name: "finance_graph_get_goal_contribution_history");
        yield return AIFunctionFactory.Create(tools.GetAccountStatement, name: "finance_graph_get_account_statement");
        yield return AIFunctionFactory.Create(tools.GetPartyObligationSummary, name: "finance_graph_get_party_obligation_summary");
    }
}
