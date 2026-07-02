using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Reporting;

namespace Aonik.Commerce.Services.Reporting;

/// <summary>
/// The margin &amp; profit report for the Commerce module (Spec 057): for a window, per
/// <c>ProductVariant</c> and in aggregate — discounted revenue (from payment-completed
/// <c>ProductPurchase</c> orders + their Spec 042 <c>OrderChargeSummary</c>), standard-cost COGS
/// (the Spec 051 rollup × quantity sold), gross margin, margin %, and variance against the
/// product's target margin. A pure read projection — revenue comes from the Ordering spine, never
/// a Finance ledger query. The only write this spec owns is setting
/// <c>Product.TargetMarginPct</c>, kept here so the margin feature service owns its commercial
/// write (mirroring <c>IProductPricingService.SetPriceAsync</c>).
/// </summary>
public interface IMarginReportService
{
    /// <summary>
    /// Computes the margin report for the half-open UTC window (Spec 055 window semantics:
    /// <c>FromUtc &lt;= Order.CreatedAt &lt; ToUtc</c>) in one <paramref name="currency"/>.
    /// Revenue counts ONLY payment-completed orders (§8 — status <c>Complete</c>, the transition
    /// payment completion applies); COGS values quantity sold at the live Spec 051 standard cost.
    /// Rows whose COGS cannot be computed are surfaced with <c>CogsKnown = false</c> and excluded
    /// from the aggregate margin — never counted as zero cost.
    /// </summary>
    Task<MarginReportDto> GetMarginReportAsync(ProductionWindow window, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a product's target gross-margin percentage (0–100, rounded to 2 dp) — the value the
    /// report flags achieved margin against. Null clears the target (the product is never
    /// flagged). The only mutation in Spec 057.
    /// </summary>
    Task<TargetMarginDto> SetTargetMarginAsync(Guid productId, decimal? targetMarginPct, CancellationToken cancellationToken = default);
}
