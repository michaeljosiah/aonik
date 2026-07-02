using Aonik.Commerce.Contracts.Models.Production;

namespace Aonik.Commerce.Contracts.Models.Reporting;

// Spec 057 — the margin & profit report read models. There is NO persisted entity behind the
// report: it is a live projection computed on read from payment-completed ProductPurchase orders
// (via the SharedKernel Ordering contract + the Spec 042 OrderChargeSummary), Commerce bundle
// selections, and the Spec 051 standard-cost rollup. The only persisted state this spec touches is
// Product.TargetMarginPct. Reuses the Spec 055 ProductionWindow (half-open UTC [FromUtc, ToUtc)).

/// <summary>
/// One margin-report row: what one product variant sold for versus what it cost to make
/// (Spec 057 §7/§9). All money values are in the report currency. <see cref="Revenue"/> is the
/// DISCOUNTED goods revenue attributed to the variant (order-level discount apportioned pro-rata
/// by line amount; tax excluded). <see cref="Cogs"/>/<see cref="GrossMargin"/>/<see cref="MarginPct"/>
/// are null when <see cref="CogsKnown"/> is false — no active recipe, or a component without an
/// effective cost in the report currency (the Spec 050/051 diagnostic) — never a phantom zero cost;
/// such a row is excluded from the aggregate margin and its revenue reported under
/// <c>UnknownCogsRevenue</c>. <see cref="MarginPct"/> is a percentage on the 0–100 scale (2 dp),
/// directly comparable to <see cref="TargetMarginPct"/>; null when revenue is zero.
/// <see cref="IsBundle"/> marks a row whose figures include build-your-own-box attribution — either
/// a component variant expanded from a bundle line, or an unexpanded bundle line surfaced as its
/// own flagged row (v1-minimum, R6). <see cref="BelowTarget"/> is null when either the achieved
/// margin or the product target is unknown; true/false only when both are known.
/// </summary>
public record MarginReportRowDto(
    Guid ProductVariantId,
    string ProductName,
    string VariantName,
    decimal QuantitySold,
    decimal Revenue,
    decimal? Cogs,
    decimal? GrossMargin,
    decimal? MarginPct,
    bool CogsKnown,
    bool IsBundle,
    decimal? TargetMarginPct,
    bool? BelowTarget);

/// <summary>
/// The window aggregate (Spec 057 §9). <see cref="Revenue"/> sums ALL rows;
/// <see cref="KnownCogsRevenue"/>, <see cref="Cogs"/>, <see cref="GrossMargin"/> and
/// <see cref="MarginPct"/> cover COGS-known rows ONLY — a row with unknown COGS is never folded in
/// as zero cost (which would inflate reported profit); its revenue is surfaced under
/// <see cref="UnknownCogsRevenue"/> (= Revenue − KnownCogsRevenue) so coverage gaps stay visible.
/// <see cref="MarginPct"/> is on the 0–100 scale (2 dp); null when no COGS-known revenue exists.
/// </summary>
public record MarginAggregateDto(
    decimal Revenue,
    decimal KnownCogsRevenue,
    decimal Cogs,
    decimal GrossMargin,
    decimal? MarginPct,
    decimal UnknownCogsRevenue);

/// <summary>
/// The margin &amp; profit report for a window in one currency (Spec 057 §11). Rows are per
/// product variant (bundle lines component-expanded, §8). <see cref="VariantsWithoutRecipe"/> and
/// <see cref="VariantsWithUnknownCost"/> surface the Spec 050/051 diagnostics behind the
/// CogsKnown = false rows (no active recipe vs. recipe present but a component cost missing in the
/// report currency). <see cref="OrdersExcludedByCurrency"/> counts payment-completed orders in the
/// window whose currency differs from the report currency — skipped (Commerce holds no FX), never
/// silently dropped.
/// </summary>
public record MarginReportDto(
    ProductionWindow Window,
    string Currency,
    IReadOnlyList<MarginReportRowDto> Rows,
    MarginAggregateDto Aggregate,
    IReadOnlyList<Guid> VariantsWithoutRecipe,
    IReadOnlyList<Guid> VariantsWithUnknownCost,
    int OrdersExcludedByCurrency);

/// <summary>A product's target gross-margin percentage after a set/clear (Spec 057 §10).</summary>
public record TargetMarginDto(Guid ProductId, string ProductName, decimal? TargetMarginPct);
