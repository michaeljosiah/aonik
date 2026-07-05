using System.ComponentModel;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Finance;

namespace Aonik.Finance.Agents.Tools;

/// <summary>
/// Personal-finance dashboard + FX-rate read tools. FX history is read through the
/// cross-module <see cref="IFxRateHistoryReader"/> contract, so the tool takes no
/// dependency on Finance's pricing service. Registered by
/// <see cref="PersonalFinanceTools.CreateAll"/>.
/// </summary>
internal sealed class PersonalFinanceDashboardTools
{
    private readonly IDashboardService _dashboardService;
    private readonly IFxRateHistoryReader _fxRateHistoryReader;

    public PersonalFinanceDashboardTools(
        IDashboardService dashboardService,
        IFxRateHistoryReader fxRateHistoryReader)
    {
        _dashboardService = dashboardService;
        _fxRateHistoryReader = fxRateHistoryReader;
    }

    // ── Dashboard Read Tool ───────────────────────────────────────

    [Description("Gets the personal finance dashboard overview. Returns aggregated metrics (net worth, available to spend, assets, bills due), upcoming bills, recent orders, and a monthly spending breakdown.")]
    public async Task<DashboardResponse> GetDashboard(
        CancellationToken cancellationToken = default)
    {
        return await _dashboardService.GetDashboardAsync(cancellationToken);
    }

    // ── FX Rate Read Tool ─────────────────────────────────────────

    [Description("Gets historical FX rate data for a currency pair over the past N days. Returns daily rate points and a buy/hold/wait timing signal. Use this to fetch real rate data before calling the display_fx_rate_chart frontend tool.")]
    public async Task<FxRateHistory> GetFxRateHistory(
        [Description("ISO 4217 base currency code (e.g., 'GBP')")] string baseCurrency,
        [Description("ISO 4217 target currency code (e.g., 'NGN')")] string targetCurrency,
        [Description("Number of days of history to fetch (default: 7)")] int days = 7,
        CancellationToken cancellationToken = default)
    {
        return await _fxRateHistoryReader.GetRateHistoryAsync(baseCurrency, targetCurrency, days, cancellationToken);
    }
}
