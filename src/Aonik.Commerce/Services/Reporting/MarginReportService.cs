using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Contracts.Models.Reporting;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Production;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Reporting;

/// <summary>
/// Spec 057 — the profit report, computed on read. No entity, no DbSet, no snapshot: the report is
/// a projection over data AONIK already holds — revenue from the Order spine (via
/// <see cref="IOrderService.ListWithItemsAsync"/>, the Spec 055 read path) joined to the durable
/// Spec 042 <c>OrderChargeSummary</c>, COGS from the Spec 051 standard-cost rollup, the target from
/// <c>Product.TargetMarginPct</c>. The §8 revenue-inclusion rule — the spec's chief honesty risk —
/// is centralised HERE and differs deliberately from Spec 055's demand set: the kitchen cooks for
/// COMMITTED orders (Pending…Complete), but profit counts only PAID ones. Payment completion is
/// signalled on the spine (<c>CommercePaymentCompletedHandler</c> → <c>ConfirmPaymentAsync</c>
/// transitions the order to <c>Complete</c>); <c>OrderChargeSummary.PaymentStatus</c> is the
/// intent's initial status written once at checkout and never updated, so it is NOT the completion
/// signal (§8).
/// </summary>
internal sealed class MarginReportService : IMarginReportService
{
    /// <summary>Window guard (mirrors Spec 055 §12): bounds the spine query so a fat-fingered
    /// range cannot scan an unbounded order history. 92 days covers a quarter.</summary>
    private const int MaxWindowDays = 92;

    /// <summary>The spine caps a list page at 200; the report consumes every page anyway.</summary>
    private const int RevenuePageSize = 200;

    private const string UnknownProductName = "(unknown product)";
    private const string UnknownVariantName = "(unknown variant)";
    private const string UnexpandedBundleVariantName = "(bundle — components not expanded)";

    private readonly CommerceDbContext _dbContext;
    private readonly IOrderService _orders;
    private readonly IProductCostingService _costing;
    private readonly IProductPricingService _pricing;
    private readonly ITenantProvider _tenantProvider;

    public MarginReportService(
        CommerceDbContext dbContext,
        IOrderService orders,
        IProductCostingService costing,
        IProductPricingService pricing,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _orders = orders;
        _costing = costing;
        _pricing = pricing;
        _tenantProvider = tenantProvider;
    }

    public async Task<MarginReportDto> GetMarginReportAsync(ProductionWindow window, string currency, CancellationToken cancellationToken = default)
    {
        ValidateWindow(window);
        // ONE normalized report currency (the Spec 051 convention: reject null/empty, trim,
        // uppercase), used for the order filter, the COGS rollup AND the bundle price lookups.
        // ResolvePriceAsync matches ProductPrice.Currency EXACTLY, so a raw "ngn" would silently
        // miss every component's standalone price and degrade the §8 value-weighted bundle split
        // to the quantity fallback — misreported per-variant revenue, not an error.
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("A report currency is required.", nameof(currency));
        }
        var reportCurrency = currency.Trim().ToUpperInvariant();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var (orders, ordersExcludedByCurrency) = await ReadRevenueOrdersAsync(window, reportCurrency, cancellationToken);
        var orderIds = orders.Select(o => o.Id).ToList();

        var summariesByOrder = await LoadChargeSummariesAsync(tenantId, orderIds, cancellationToken);
        var selectionsByLine = await LoadBundleSelectionsAsync(tenantId, orderIds, cancellationToken);

        // ── attribute revenue + quantity per variant (§8) ────────────────────────────────────────
        var byVariant = new Dictionary<Guid, VariantAccumulator>();

        foreach (var order in orders)
        {
            // Revenue basis: the DISCOUNTED goods total. Line amounts are the goods (subtotal);
            // the order-level DiscountTotal from the durable charge summary is apportioned to the
            // lines pro-rata by line amount, so per-variant revenue sums exactly to
            // Subtotal − DiscountTotal. Tax is excluded (pass-through, § 8). An order without a
            // charge summary (created outside Commerce checkout) contributes its line amounts
            // undiscounted — there is no discount record to apportion.
            var discountTotal = summariesByOrder.TryGetValue(order.Id, out var summary)
                ? summary.DiscountTotal
                : 0m;

            var items = order.Items.OrderBy(i => i.ItemIndex).ToList();
            var discountShares = AllocateProportionally(discountTotal, items.Select(i => i.AmountIn).ToList());

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var lineRevenue = item.AmountIn - discountShares[i];

                // A line WITH OrderBundleSelection rows is a build-your-own-box (Spec 042 §12
                // Option A — OrderItem.ProductId is the BUNDLE PRODUCT id): expand it, attributing
                // the line's discounted revenue across the chosen components (§8). Selection
                // Quantity is already the line TOTAL (selection × boxes, as checkout wrote it).
                if (selectionsByLine.TryGetValue((order.Id, item.ItemIndex), out var selections))
                {
                    await ExpandBundleLineAsync(byVariant, selections, lineRevenue, reportCurrency, cancellationToken);
                    continue;
                }

                // Simple/variant demand — the Spec 055 line-inclusion rule (checkout writes both
                // fields on every retail line).
                if (item.ProductId is not { } variantId || item.Quantity is not { } quantity)
                {
                    continue;
                }
                Add(byVariant, variantId, quantity, lineRevenue, viaBundle: false);
            }
        }

        // ── resolve identity + target, value at standard cost, compose rows (§9/§10) ─────────────
        var resolution = await ResolveVariantsAsync(tenantId, byVariant.Keys, cancellationToken);

        var variantsWithoutRecipe = new List<Guid>();
        var variantsWithUnknownCost = new List<Guid>();
        var rows = new List<MarginReportRowDto>(byVariant.Count);

        foreach (var (variantId, acc) in byVariant)
        {
            decimal? unitCost = null;
            if (resolution.Variants.TryGetValue(variantId, out var resolved))
            {
                // COGS = live Spec 051 standard cost × quantity sold, in the report currency. The
                // rollup withholds the total (UnitCost = null) when the variant has no active
                // recipe or any component lacks an effective cost in that currency — surfaced
                // below as CogsKnown = false, never a silent zero (§9/R5).
                var rollup = await _costing.RollupStandardCostAsync(variantId, reportCurrency, atUtc: null, cancellationToken);
                unitCost = rollup.UnitCost;
                if (!rollup.HasActiveRecipe)
                {
                    variantsWithoutRecipe.Add(variantId);
                }
                else if (!rollup.CostComplete)
                {
                    variantsWithUnknownCost.Add(variantId);
                }
            }

            var (productName, variantName, targetMarginPct, isUnexpandedBundle) = DescribeRow(variantId, resolution);

            var cogsKnown = unitCost is not null;
            var cogs = unitCost is { } uc ? Round4(uc * acc.Quantity) : (decimal?)null;
            var grossMargin = cogs is { } c ? acc.Revenue - c : (decimal?)null;
            // Percentage on the 0–100 scale (2 dp), directly comparable to TargetMarginPct; null
            // when revenue is zero (R5) or COGS unknown.
            var marginPct = grossMargin is { } gm && acc.Revenue > 0m
                ? Round2(gm / acc.Revenue * 100m)
                : (decimal?)null;
            var belowTarget = marginPct is { } achieved && targetMarginPct is { } target
                ? achieved < target
                : (bool?)null;

            rows.Add(new MarginReportRowDto(
                variantId,
                productName,
                variantName,
                acc.Quantity,
                acc.Revenue,
                cogs,
                grossMargin,
                marginPct,
                cogsKnown,
                acc.ViaBundle || isUnexpandedBundle,
                targetMarginPct,
                belowTarget));
        }

        rows = rows
            .OrderBy(r => r.ProductName, StringComparer.Ordinal)
            .ThenBy(r => r.VariantName, StringComparer.Ordinal)
            .ThenBy(r => r.ProductVariantId)
            .ToList();

        return new MarginReportDto(
            window,
            reportCurrency,
            rows,
            Aggregate(rows),
            variantsWithoutRecipe,
            variantsWithUnknownCost,
            ordersExcludedByCurrency);
    }

    public async Task<TargetMarginDto> SetTargetMarginAsync(Guid productId, decimal? targetMarginPct, CancellationToken cancellationToken = default)
    {
        // Range enforced here (the 5,2 column precision is belt-and-braces the InMemory provider
        // cannot prove); stored at 2 dp so the persisted value matches SQL Server exactly.
        if (targetMarginPct is { } pct && (pct < 0m || pct > 100m))
        {
            throw new ArgumentException("TargetMarginPct must be a percentage between 0 and 100.", nameof(targetMarginPct));
        }
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var product = await _dbContext.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Product '{productId}' was not found.");

        product.TargetMarginPct = targetMarginPct is { } value ? Round2(value) : null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TargetMarginDto(product.Id, product.Name, product.TargetMarginPct);
    }

    // ── §8 revenue read ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Every ProductPurchase order in the half-open CreatedAt window whose status is
    /// <c>Complete</c> — the transition <c>ConfirmPaymentAsync</c> applies exactly on payment
    /// completion (PaymentCompletedEvent), i.e. actual money. Deliberately narrower than Spec
    /// 055's demand set (Pending/UnderReview/Approved/Transmitted/Complete): the production sheet
    /// cooks for committed orders; the profit report counts only funded ones. Orders in another
    /// currency are skipped and counted (Commerce holds no FX; ₦ and £ cannot be summed).
    /// </summary>
    private async Task<(List<OrderDto> Included, int ExcludedByCurrency)> ReadRevenueOrdersAsync(
        ProductionWindow window, string currency, CancellationToken cancellationToken)
    {
        var included = new List<OrderDto>();
        var excludedByCurrency = 0;
        var pageNumber = 1;
        while (true)
        {
            var page = await _orders.ListWithItemsAsync(
                new ListOrdersQuery(
                    OrderType: OrderTypeCodes.ProductPurchase,
                    Status: OrderStatusCodes.Complete,
                    PageNumber: pageNumber,
                    PageSize: RevenuePageSize,
                    CreatedFromUtc: window.FromUtc,
                    CreatedToUtc: window.ToUtc),
                cancellationToken);

            foreach (var order in page.Items)
            {
                if (string.Equals(order.CurrencyIn, currency, StringComparison.OrdinalIgnoreCase))
                {
                    included.Add(order);
                }
                else
                {
                    excludedByCurrency++;
                }
            }

            if (pageNumber * RevenuePageSize >= page.TotalCount)
            {
                return (included, excludedByCurrency);
            }
            pageNumber++;
        }
    }

    private async Task<Dictionary<Guid, Entities.Promotions.OrderChargeSummary>> LoadChargeSummariesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return new Dictionary<Guid, Entities.Promotions.OrderChargeSummary>();
        }
        var summaries = await _dbContext.OrderChargeSummaries
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
            .ToListAsync(cancellationToken);
        return summaries
            .GroupBy(s => s.OrderId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    private async Task<Dictionary<(Guid OrderId, int OrderItemIndex), List<OrderBundleSelection>>> LoadBundleSelectionsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        var selectionsByLine = new Dictionary<(Guid OrderId, int OrderItemIndex), List<OrderBundleSelection>>();
        if (orderIds.Count == 0)
        {
            return selectionsByLine;
        }
        var selectionRows = await _dbContext.OrderBundleSelections
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
            .ToListAsync(cancellationToken);
        foreach (var group in selectionRows.GroupBy(s => (s.OrderId, s.OrderItemIndex)))
        {
            selectionsByLine[group.Key] = group.ToList();
        }
        return selectionsByLine;
    }

    /// <summary>
    /// §8 — a bundle line's discounted revenue split across its chosen components pro-rata by
    /// component value. OrderBundleSelection persists no price snapshot, so v1's split key is the
    /// component's relative standalone selling price — the CURRENT active list price in the report
    /// currency × selection quantity — falling back to pro-rata by quantity when any component
    /// price is unresolvable (still deterministic; sums still reconcile). The exact key remains an
    /// Open refinement.
    /// </summary>
    private async Task ExpandBundleLineAsync(
        Dictionary<Guid, VariantAccumulator> byVariant,
        List<OrderBundleSelection> selections,
        decimal lineRevenue,
        string currency,
        CancellationToken cancellationToken)
    {
        var weights = new List<decimal>(selections.Count);
        foreach (var selection in selections)
        {
            var unitPrice = await _pricing.ResolvePriceAsync(selection.ProductVariantId, currency, null, cancellationToken);
            if (unitPrice is not { } price)
            {
                weights = selections.Select(s => s.Quantity).ToList();
                break;
            }
            weights.Add(price * selection.Quantity);
        }

        var shares = AllocateProportionally(lineRevenue, weights);
        for (var i = 0; i < selections.Count; i++)
        {
            Add(byVariant, selections[i].ProductVariantId, selections[i].Quantity, shares[i], viaBundle: true);
        }
    }

    private static void Add(
        Dictionary<Guid, VariantAccumulator> byVariant, Guid variantId, decimal quantity, decimal revenue, bool viaBundle)
    {
        var acc = byVariant.TryGetValue(variantId, out var existing) ? existing : new VariantAccumulator();
        acc.Quantity += quantity;
        acc.Revenue += revenue;
        acc.ViaBundle |= viaBundle;
        byVariant[variantId] = acc;
    }

    private sealed class VariantAccumulator
    {
        public decimal Quantity;
        public decimal Revenue;
        public bool ViaBundle;
    }

    // ── §8 pro-rata allocation ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Splits <paramref name="total"/> across <paramref name="weights"/> proportionally, each
    /// share rounded to 4 dp away from zero, with the rounding remainder assigned to the
    /// largest-weight row (ties → first) — so the shares always sum to <paramref name="total"/>
    /// EXACTLY and per-variant revenue reconciles to the order's discounted total (§8). A
    /// non-positive weight sum falls back to equal weights (a corner-case backstop; checkout never
    /// writes zero-amount lines).
    /// </summary>
    private static decimal[] AllocateProportionally(decimal total, IReadOnlyList<decimal> weights)
    {
        var shares = new decimal[weights.Count];
        if (weights.Count == 0 || total == 0m)
        {
            return shares;
        }

        var effective = weights;
        var weightSum = weights.Sum();
        if (weightSum <= 0m)
        {
            effective = Enumerable.Repeat(1m, weights.Count).ToList();
            weightSum = weights.Count;
        }

        var allocated = 0m;
        var largestIndex = 0;
        for (var i = 0; i < effective.Count; i++)
        {
            shares[i] = Round4(total * effective[i] / weightSum);
            allocated += shares[i];
            if (effective[i] > effective[largestIndex])
            {
                largestIndex = i;
            }
        }
        shares[largestIndex] += total - allocated;
        return shares;
    }

    // ── identity + target resolution (LEFT-join semantics, Spec 055 §9) ─────────────────────────

    private sealed record ResolvedVariant(string ProductName, string VariantName, decimal? TargetMarginPct);

    private sealed record VariantResolution(
        Dictionary<Guid, ResolvedVariant> Variants,
        Dictionary<Guid, (string Name, string Kind)> UnresolvedProducts);

    /// <summary>
    /// Resolves row keys against the live catalog. A key that resolves to a ProductVariant carries
    /// its product's name + TargetMarginPct. A key that instead resolves to a PRODUCT id is an
    /// unexpanded bundle line (Spec 042 §12 — OrderItem.ProductId is the bundle product; no
    /// selection rows survived) surfaced as a flagged row, never silently mis-joined (R6). A key
    /// resolving to neither still shows, with diagnostic placeholder names — never dropped.
    /// </summary>
    private async Task<VariantResolution> ResolveVariantsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return new VariantResolution(new Dictionary<Guid, ResolvedVariant>(), new Dictionary<Guid, (string, string)>());
        }

        var ids = variantIds.ToList();
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && ids.Contains(v.Id))
            .Select(v => new { v.Id, v.Name, v.ProductId })
            .ToListAsync(cancellationToken);

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.TargetMarginPct })
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var resolved = variants.ToDictionary(
            v => v.Id,
            v => products.TryGetValue(v.ProductId, out var product)
                ? new ResolvedVariant(product.Name, v.Name, product.TargetMarginPct)
                : new ResolvedVariant(UnknownProductName, v.Name, null));

        var unresolvedIds = ids.Where(id => !resolved.ContainsKey(id)).ToList();
        var unresolvedProducts = unresolvedIds.Count == 0
            ? new Dictionary<Guid, (string Name, string Kind)>()
            : await _dbContext.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && unresolvedIds.Contains(p.Id))
                .Select(p => new { p.Id, p.Name, p.Kind })
                .ToDictionaryAsync(p => p.Id, p => (p.Name, p.Kind), cancellationToken);

        return new VariantResolution(resolved, unresolvedProducts);
    }

    private static (string ProductName, string VariantName, decimal? TargetMarginPct, bool IsUnexpandedBundle) DescribeRow(
        Guid variantId, VariantResolution resolution)
    {
        if (resolution.Variants.TryGetValue(variantId, out var variant))
        {
            return (variant.ProductName, variant.VariantName, variant.TargetMarginPct, false);
        }
        if (resolution.UnresolvedProducts.TryGetValue(variantId, out var product))
        {
            // The row keys on a PRODUCT id — an unexpanded bundle line (or a mis-typed simple
            // product line): flagged by name + IsBundle, CogsKnown stays false (v1-minimum, R6).
            return (product.Name, UnexpandedBundleVariantName, null, string.Equals(product.Kind, ProductKinds.Bundle, StringComparison.Ordinal));
        }
        return (UnknownProductName, UnknownVariantName, null, false);
    }

    // ── §9 aggregate ────────────────────────────────────────────────────────────────────────────

    /// <summary>COGS-known rows only for the margin figures; unknown-COGS revenue surfaced,
    /// never folded in as zero cost (the aggregate would otherwise overstate profit — R5).</summary>
    private static MarginAggregateDto Aggregate(IReadOnlyList<MarginReportRowDto> rows)
    {
        var revenue = rows.Sum(r => r.Revenue);
        var knownRows = rows.Where(r => r.CogsKnown).ToList();
        var knownCogsRevenue = knownRows.Sum(r => r.Revenue);
        var cogs = knownRows.Sum(r => r.Cogs ?? 0m);
        var grossMargin = knownCogsRevenue - cogs;
        var marginPct = knownCogsRevenue > 0m
            ? Round2(grossMargin / knownCogsRevenue * 100m)
            : (decimal?)null;

        return new MarginAggregateDto(
            revenue,
            knownCogsRevenue,
            cogs,
            grossMargin,
            marginPct,
            UnknownCogsRevenue: revenue - knownCogsRevenue);
    }

    // ── window guard (mirrors Spec 055 §12) ─────────────────────────────────────────────────────

    private static void ValidateWindow(ProductionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.FromUtc >= window.ToUtc)
        {
            throw new ArgumentException(
                "The report window must satisfy FromUtc < ToUtc (half-open [FromUtc, ToUtc)).", nameof(window));
        }
        if (window.ToUtc - window.FromUtc > TimeSpan.FromDays(MaxWindowDays))
        {
            throw new ArgumentException(
                $"The report window may span at most {MaxWindowDays} days.", nameof(window));
        }
    }

    private static decimal Round4(decimal value) => Math.Round(value, 4, MidpointRounding.AwayFromZero);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
