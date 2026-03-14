using System.ComponentModel;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using ModelContextProtocol.Server;

namespace Aonik.Finance.Mcp.Tools;

[McpServerToolType]
public static class FinancialLifeGraphMcpTools
{
    [McpServerTool(Name = "finance_get_financial_life_graph_summary"), Description("Returns a compact financial life graph summary for the current user.")]
    public static async Task<FinancialLifeGraphSummaryResponse> GetFinancialLifeGraphSummary(
        IFinancialLifeGraphService financialLifeGraphService,
        CancellationToken cancellationToken = default)
    {
        return await financialLifeGraphService.GetGraphSummaryAsync(cancellationToken);
    }

    [McpServerTool(Name = "finance_get_upcoming_obligations"), Description("Returns upcoming obligations across bills, subscriptions, and goals for the current user.")]
    public static async Task<IReadOnlyList<UpcomingObligationResponse>> GetUpcomingObligations(
        IFinancialLifeGraphService financialLifeGraphService,
        [Description("Number of days ahead to inspect")] int withinDays = 30,
        CancellationToken cancellationToken = default)
    {
        return await financialLifeGraphService.GetUpcomingObligationsAsync(withinDays, cancellationToken);
    }

    [McpServerTool(Name = "finance_get_financial_life_graph"), Description("Returns the full financial life graph for the current user, including nodes, edges, and source coverage.")]
    public static async Task<FinancialLifeGraphResponse> GetFinancialLifeGraph(
        IFinancialLifeGraphService financialLifeGraphService,
        CancellationToken cancellationToken = default)
    {
        return await financialLifeGraphService.GetGraphAsync(cancellationToken);
    }
}
