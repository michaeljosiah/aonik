using System.ComponentModel;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.PersonalFinance.Agents.Tools;

internal sealed class FinancialLifeGraphTools
{
    private readonly IFinancialLifeGraphService _financialLifeGraphService;

    private FinancialLifeGraphTools(IFinancialLifeGraphService financialLifeGraphService)
    {
        _financialLifeGraphService = financialLifeGraphService;
    }

    [Description("Returns a compact financial life graph summary for the current user, including accounts, transactions, bills, goals, subscriptions, household membership, and related parties.")]
    public async Task<FinancialLifeGraphSummaryResponse> GetFinancialLifeGraphSummary(CancellationToken cancellationToken = default)
    {
        return await _financialLifeGraphService.GetGraphSummaryAsync(cancellationToken);
    }

    [Description("Returns upcoming financial obligations for the current user across bills, subscriptions, and dated goals.")]
    public async Task<IReadOnlyList<UpcomingObligationResponse>> GetUpcomingObligations(
        [Description("Number of days ahead to inspect for upcoming obligations")] int withinDays = 30,
        CancellationToken cancellationToken = default)
    {
        return await _financialLifeGraphService.GetUpcomingObligationsAsync(withinDays, cancellationToken);
    }

    [Description("Returns the full financial life graph read model for the current user, including nodes, edges, and source coverage.")]
    public async Task<FinancialLifeGraphResponse> GetFinancialLifeGraph(CancellationToken cancellationToken = default)
    {
        return await _financialLifeGraphService.GetGraphAsync(cancellationToken);
    }

    [Description("Returns household-specific finance graph context for the current user, including household and member nodes when a household exists.")]
    public async Task<HouseholdFinanceContextResponse> GetHouseholdFinanceContext(CancellationToken cancellationToken = default)
    {
        return await _financialLifeGraphService.GetHouseholdFinanceContextAsync(cancellationToken);
    }

    [Description("Returns related-party finance context for the current user, including related parties and relationship metadata.")]
    public async Task<RelatedPartyFinanceContextResponse> GetRelatedPartyFinanceContext(CancellationToken cancellationToken = default)
    {
        return await _financialLifeGraphService.GetRelatedPartyFinanceContextAsync(cancellationToken);
    }

    public static IEnumerable<AITool> CreateAll(IServiceProvider serviceProvider)
    {
        var tools = new FinancialLifeGraphTools(serviceProvider.GetRequiredService<IFinancialLifeGraphService>());

        yield return AIFunctionFactory.Create(tools.GetFinancialLifeGraphSummary, name: "finance_get_financial_life_graph_summary");
        yield return AIFunctionFactory.Create(tools.GetUpcomingObligations, name: "finance_get_upcoming_obligations");
        yield return AIFunctionFactory.Create(tools.GetFinancialLifeGraph, name: "finance_get_financial_life_graph");
        yield return AIFunctionFactory.Create(tools.GetHouseholdFinanceContext, name: "finance_get_household_finance_context");
        yield return AIFunctionFactory.Create(tools.GetRelatedPartyFinanceContext, name: "finance_get_related_party_finance_context");
    }
}
