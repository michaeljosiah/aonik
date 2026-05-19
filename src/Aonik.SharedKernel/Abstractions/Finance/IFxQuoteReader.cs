namespace Aonik.SharedKernel.Abstractions.Finance;

/// <summary>
/// Cross-module read access to recent FX quotes.
/// PersonalFinance's FinancialLifeGraph hydration consumes this to expose
/// "relevant fx context" without depending on <c>Aonik.Finance.Entities.Pricing.FxQuote</c>.
/// See <a href="../../../../docs/specifications/027.extract-personal-finance-module.html">Spec 027</a>.
/// </summary>
public interface IFxQuoteReader
{
    /// <summary>
    /// Returns the most recently-quoted FxQuotes between any pair drawn from
    /// the supplied currency set, scoped to the tenant. Limit caps the result
    /// count.
    /// </summary>
    Task<IReadOnlyList<FxQuoteHistoryItem>> GetRecentForCurrenciesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> currencies,
        int limit,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cross-module projection of an FxQuote.
/// </summary>
public sealed record FxQuoteHistoryItem(
    Guid QuoteId,
    string BaseCurrency,
    string TargetCurrency,
    decimal Rate,
    DateTime ExpiresAt,
    DateTime QuotedAt,
    string? Provider);
