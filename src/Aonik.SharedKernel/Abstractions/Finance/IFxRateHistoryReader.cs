namespace Aonik.SharedKernel.Abstractions.Finance;

/// <summary>
/// Cross-module read access to historical FX rates for a currency pair, with a buy / hold / wait
/// timing signal. Consumed by PersonalFinance's "Simi" FX tool to fetch real rate data before it
/// renders a rate chart, without depending on <c>Aonik.Finance.Contracts.Services.Pricing</c>.
/// Distinct from <see cref="IFxQuoteReader"/> (recent executable quotes across a currency set);
/// this returns a dated series for one pair. The implementation lives in Aonik.Finance over its
/// pricing service. See
/// <a href="../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface IFxRateHistoryReader
{
    /// <summary>
    /// Returns up to <paramref name="days"/> of daily rate points for
    /// <paramref name="baseCurrency"/> → <paramref name="targetCurrency"/>, plus a timing signal.
    /// </summary>
    Task<FxRateHistory> GetRateHistoryAsync(
        string baseCurrency,
        string targetCurrency,
        int days,
        CancellationToken cancellationToken = default);
}

/// <summary>A dated FX-rate series for one currency pair with a buy / hold / wait timing signal.</summary>
public sealed record FxRateHistory(
    string BaseCurrency,
    string TargetCurrency,
    IReadOnlyList<FxRateHistoryPoint> Rates,
    string Signal,
    string SignalReason);

/// <summary>One point on an <see cref="FxRateHistory"/> series (<paramref name="Date"/> is an
/// ISO-8601 date string, matching the pricing service's own projection).</summary>
public sealed record FxRateHistoryPoint(string Date, decimal Rate);
