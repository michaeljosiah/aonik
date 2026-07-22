using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Inventory;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Production;

/// <summary>
/// Spec 055 — production planning, computed on read. No entity, no DbSet, no migration: the sheet
/// is an aggregation over the Order spine (via <see cref="IOrderService.ListWithItemsAsync"/> —
/// Commerce reads the orders it created, never through a Finance reference), and the prep list is
/// that sheet composed with Spec 050's explosion primitive and Spec 052's stock reads. The §9
/// order-inclusion filter — the spec's chief correctness risk — is centralised HERE
/// (<see cref="DemandStatuses"/> + the window bounds pushed into <see cref="ListOrdersQuery"/>)
/// and nowhere else.
/// </summary>
internal sealed class ProductionPlanningService : IProductionPlanningService
{
    /// <summary>Window guard (§12): bounds the spine query so a fat-fingered range cannot scan an
    /// unbounded order history.</summary>
    private const int MaxWindowDays = 92;

    /// <summary>The spine caps a list page at 200; the aggregation consumes every page anyway, so
    /// it pages at the cap.</summary>
    private const int DemandPageSize = 200;

    private const string UnknownProductName = "(unknown product)";
    private const string UnknownVariantName = "(unknown variant)";

    /// <summary>
    /// §9 — the statuses that count as demand: committed, non-terminal-failed orders. Draft is
    /// excluded because checkout creates the ProductPurchase order in Draft and only payment
    /// completion advances it (Spec 042 §11) — a Draft is an unpaid checkout intent, not demand
    /// the kitchen should cook for. Cancelled/Failed/Expired are the terminal failures
    /// (<see cref="OrderStatusCodes.IsTerminal"/> minus Complete).
    /// </summary>
    private static readonly string[] DemandStatuses =
    [
        OrderStatusCodes.Pending,
        OrderStatusCodes.UnderReview,
        OrderStatusCodes.Approved,
        OrderStatusCodes.Transmitted,
        OrderStatusCodes.Complete,
    ];

    private readonly CommerceDbContext _dbContext;
    private readonly IOrderService _orders;
    private readonly IRecipeService _recipes;
    private readonly IInventoryService _inventory;
    private readonly ITenantProvider _tenantProvider;

    public ProductionPlanningService(
        CommerceDbContext dbContext,
        IOrderService orders,
        IRecipeService recipes,
        IInventoryService inventory,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _orders = orders;
        _recipes = recipes;
        _inventory = inventory;
        _tenantProvider = tenantProvider;
    }

    public async Task<ProductionSheetDto> GetProductionSheetAsync(ProductionWindow window, CancellationToken cancellationToken = default)
    {
        ValidateWindow(window);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var included = await ReadDemandOrdersAsync(window, cancellationToken);

        // Bundle order lines carry the BUNDLE PRODUCT id on OrderItem.ProductId (Spec 042 §12
        // Option A); the chosen components live in Commerce's OrderBundleSelection rows, keyed by
        // (OrderId, OrderItemIndex), with Quantity already the line TOTAL (selection × boxes, as
        // checkout wrote it). A line WITH selection rows is expanded — the components are the real
        // kitchen demand; a line without them is simple/variant demand via its ProductId.
        var orderIds = included.Select(o => o.Id).ToList();
        var selectionsByLine = new Dictionary<(Guid OrderId, int OrderItemIndex), List<OrderBundleSelection>>();
        if (orderIds.Count > 0)
        {
            var selectionRows = await _dbContext.OrderBundleSelections
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
                .ToListAsync(cancellationToken);
            foreach (var group in selectionRows.GroupBy(s => (s.OrderId, s.OrderItemIndex)))
            {
                selectionsByLine[group.Key] = group.ToList();
            }
        }

        // Spec 068 §9 — demand groups by (variant, canonical personalisation): collapsing two
        // preparations of one variant can never be un-collapsed afterwards. Unpersonalised demand
        // keys on the empty string and renders exactly as before.
        var portionsByKey = new Dictionary<(Guid VariantId, string Personalisation), decimal>();
        var ordersByKey = new Dictionary<(Guid VariantId, string Personalisation), HashSet<Guid>>();
        var metaByKey = new Dictionary<(Guid VariantId, string Personalisation), (string? Json, string? Summary, string? Display)>();
        var bundleLinesExpanded = 0;

        void AddDemand(Guid variantId, string? personalisationJson, string? summary, string? display, decimal portions, Guid orderId)
        {
            if (portions <= 0m)
            {
                return;
            }
            var key = (variantId, personalisationJson ?? string.Empty);
            portionsByKey[key] = portionsByKey.GetValueOrDefault(key) + portions;
            if (!ordersByKey.TryGetValue(key, out var contributingOrders))
            {
                ordersByKey[key] = contributingOrders = new HashSet<Guid>();
            }
            contributingOrders.Add(orderId);
            if (!metaByKey.ContainsKey(key))
            {
                metaByKey[key] = (personalisationJson, summary, display);
            }
        }

        foreach (var order in included)
        {
            foreach (var item in order.Items)
            {
                if (selectionsByLine.TryGetValue((order.Id, item.ItemIndex), out var selections))
                {
                    bundleLinesExpanded++;
                    foreach (var selection in selections)
                    {
                        AddDemand(
                            selection.ProductVariantId,
                            selection.PersonalisationJson,
                            selection.PersonalisationSummary,
                            DisplayFromEnvelope(selection.PersonalisationEnvelopeJson),
                            selection.Quantity,
                            order.Id);
                    }
                    continue;
                }

                if (item.ProductId is not { } variantId || item.Quantity is not { } quantity)
                {
                    continue;
                }
                // Spec 071 (and 066 §12) — a personalised RETAIL line (an add-on, or a simple
                // line with selections) carries its envelope in DetailsJson: two preparations of
                // the same product must not collapse into one production row.
                var (retailJson, retailSummary, retailDisplay) = EnvelopeFacts(item.DetailsJson);
                AddDemand(variantId, retailJson, retailSummary, retailDisplay, quantity, order.Id);
            }
        }

        var names = await ResolveVariantNamesAsync(
            tenantId, portionsByKey.Keys.Select(k => k.VariantId).Distinct().ToList(), cancellationToken);

        var lines = portionsByKey
            .Select(kvp =>
            {
                var (productName, variantName) = names.TryGetValue(kvp.Key.VariantId, out var resolved)
                    ? resolved
                    : (UnknownProductName, UnknownVariantName);
                var meta = metaByKey[kvp.Key];
                return new ProductionSheetLineDto(
                    kvp.Key.VariantId, productName, variantName, kvp.Value, ordersByKey[kvp.Key].Count,
                    meta.Json, meta.Summary, meta.Display);
            })
            .OrderBy(l => l.ProductName, StringComparer.Ordinal)
            .ThenBy(l => l.VariantName, StringComparer.Ordinal)
            .ThenBy(l => l.ProductVariantId)
            .ThenBy(l => l.PersonalisationSummary ?? string.Empty, StringComparer.Ordinal)
            .ToList();

        return new ProductionSheetDto(window, lines, TotalOrders: included.Count, bundleLinesExpanded);
    }

    public async Task<PrepListDto> GetPrepListAsync(ProductionWindow window, bool netAgainstStock = true, CancellationToken cancellationToken = default)
    {
        // §10 — the prep list IS the exploded sheet: same window, same inclusion filter, and the
        // BOM math is Spec 050's primitive, never re-implemented here.
        var sheet = await GetProductionSheetAsync(window, cancellationToken);

        var demands = sheet.Lines
            .Select(l => new VariantDemand(l.ProductVariantId, l.PortionsDemanded))
            .ToList();
        var bom = await _recipes.ExplodeManyAsync(demands, cancellationToken);

        List<PrepListLineDto> lines;
        if (!netAgainstStock)
        {
            lines = bom.Lines
                .Select(l => new PrepListLineDto(
                    l.IngredientId, l.IngredientName, l.BaseUnit, l.RequiredQuantity,
                    Available: null, Shortfall: null, SuggestedOrderQuantity: null))
                .ToList();
        }
        else
        {
            var cheapestPacks = await LoadCheapestCatalogPackSizesAsync(
                bom.Lines.Select(l => l.IngredientId), cancellationToken);

            lines = new List<PrepListLineDto>(bom.Lines.Count);
            foreach (var line in bom.Lines)
            {
                // §11 — net against Spec 052's Available (OnHand − Reserved), NEVER raw OnHand:
                // 10 on hand with 8 reserved covers only 2 of a 5 requirement — shortfall 3, not
                // the false "in stock" raw on-hand would report. A never-stocked ingredient reads
                // back as zeros, so its whole requirement is the shortfall.
                var level = await _inventory.GetStockLevelAsync(
                    StockItemRef.Ingredient(line.IngredientId), cancellationToken);
                var shortfall = Math.Max(line.RequiredQuantity - level.Available, 0m);

                lines.Add(new PrepListLineDto(
                    line.IngredientId,
                    line.IngredientName,
                    line.BaseUnit,
                    line.RequiredQuantity,
                    Available: level.Available,
                    Shortfall: shortfall,
                    SuggestedOrderQuantity: SuggestOrderQuantity(
                        shortfall, level.ReorderQuantity, cheapestPacks.GetValueOrDefault(line.IngredientId))));
            }
        }

        return new PrepListDto(window, lines, bom.VariantsWithoutRecipe, netAgainstStock);
    }

    // ── §9 demand read ──────────────────────────────────────────────────────────────────────────

    /// <summary>Every ProductPurchase order in the half-open window whose status is in
    /// <see cref="DemandStatuses"/>. Type + window are pushed into the spine query (period-bounded,
    /// Spec 055 §14); the status set is applied here — the same centralised filter either way.</summary>
    private async Task<List<OrderDto>> ReadDemandOrdersAsync(ProductionWindow window, CancellationToken cancellationToken)
    {
        var included = new List<OrderDto>();
        var pageNumber = 1;
        while (true)
        {
            var page = await _orders.ListWithItemsAsync(
                new ListOrdersQuery(
                    OrderType: OrderTypeCodes.ProductPurchase,
                    PageNumber: pageNumber,
                    PageSize: DemandPageSize,
                    CreatedFromUtc: window.FromUtc,
                    CreatedToUtc: window.ToUtc),
                cancellationToken);

            included.AddRange(page.Items.Where(o => DemandStatuses.Contains(o.Status)));

            if (pageNumber * DemandPageSize >= page.TotalCount)
            {
                return included;
            }
            pageNumber++;
        }
    }

    // ── name resolution (LEFT-join semantics) ───────────────────────────────────────────────────

    private async Task<Dictionary<Guid, (string ProductName, string VariantName)>> ResolveVariantNamesAsync(
        Guid tenantId, IReadOnlyCollection<Guid> variantIds, CancellationToken cancellationToken)
    {
        if (variantIds.Count == 0)
        {
            return new Dictionary<Guid, (string ProductName, string VariantName)>();
        }

        var ids = variantIds.ToList();
        var variants = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(v => v.TenantId == tenantId && ids.Contains(v.Id))
            .Select(v => new { v.Id, v.Name, v.ProductId })
            .ToListAsync(cancellationToken);

        var productIds = variants.Select(v => v.ProductId).Distinct().ToList();
        var productNames = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return variants.ToDictionary(
            v => v.Id,
            v => (productNames.GetValueOrDefault(v.ProductId, UnknownProductName), v.Name));
    }

    // ── §11 suggested order quantity ────────────────────────────────────────────────────────────

    /// <summary>
    /// The Spec 053 shortfall-seed precedence, mirrored so the suggestion equals what
    /// <c>CreateFromShortfallAsync</c> would actually order: the level's ReorderQuantity when set
    /// (the operator's explicit suggestion, taken as-is), else the shortfall rounded UP to whole
    /// packs of the cheapest active catalog row, else the shortfall itself (just cover the gap).
    /// Null when there is nothing to order.
    /// </summary>
    private static decimal? SuggestOrderQuantity(decimal shortfall, decimal? reorderQuantity, decimal cheapestPackSize)
    {
        if (shortfall <= 0m)
        {
            return null;
        }
        if (reorderQuantity is > 0m)
        {
            return reorderQuantity;
        }
        if (cheapestPackSize > 0m)
        {
            return Math.Ceiling(shortfall / cheapestPackSize) * cheapestPackSize;
        }
        return shortfall;
    }

    /// <summary>
    /// Pack size of the cheapest-per-base-unit (PackPrice / PackSize) ACTIVE-supplier catalog row
    /// per ingredient. Commerce holds no FX, so "cheapest" is only rankable within one currency:
    /// an ingredient whose active rows span multiple currencies is skipped (no pack rounding — the
    /// suggestion falls back to the raw shortfall), a documented v1 limitation (§11).
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> LoadCheapestCatalogPackSizesAsync(
        IEnumerable<Guid> ingredientIds, CancellationToken cancellationToken)
    {
        var ids = ingredientIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, decimal>();
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var rows = await (
                from si in _dbContext.SupplierIngredients.AsNoTracking()
                join s in _dbContext.Suppliers.AsNoTracking() on si.SupplierId equals s.Id
                where si.TenantId == tenantId && s.IsActive && si.PackSize > 0m && ids.Contains(si.IngredientId)
                select new { si.IngredientId, si.PackSize, si.PackPrice, si.Currency })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.IngredientId)
            .Where(g => g.Select(r => r.Currency).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.PackPrice / r.PackSize).ThenBy(r => r.PackSize).First().PackSize);
    }

    // ── §12 window guard ────────────────────────────────────────────────────────────────────────


    /// <summary>The (canonical selection, summary, display) trio out of a Spec 066 §12 envelope
    /// carried in an order item's DetailsJson. Nulls when absent, malformed, or a different
    /// document (the Spec 068 box envelope has no canonicalSelectionJson root) — row-level
    /// degradation, never a thrown sheet.</summary>
    private static (string? Json, string? Summary, string? Display) EnvelopeFacts(string? detailsJson)
    {
        if (string.IsNullOrWhiteSpace(detailsJson))
        {
            return (null, null, null);
        }
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(detailsJson);
            if (!document.RootElement.TryGetProperty("canonicalSelectionJson", out var canonical)
                || canonical.ValueKind != System.Text.Json.JsonValueKind.String)
            {
                return (null, null, null);
            }
            var summary = document.RootElement.TryGetProperty("summary", out var s2)
                && s2.ValueKind == System.Text.Json.JsonValueKind.String ? s2.GetString() : null;
            var display = document.RootElement.TryGetProperty("display", out var d2)
                ? d2.GetRawText() : null;
            return (canonical.GetString(), summary, display);
        }
        catch (System.Text.Json.JsonException)
        {
            return (null, null, null);
        }
    }

    /// <summary>The label-snapshotted display entries out of a Spec 066 §12 envelope — raw JSON,
    /// null on absent or malformed input (row-level degradation, never a thrown sheet).</summary>
    private static string? DisplayFromEnvelope(string? envelopeJson)
    {
        if (string.IsNullOrWhiteSpace(envelopeJson))
        {
            return null;
        }
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(envelopeJson);
            return document.RootElement.TryGetProperty("display", out var display)
                ? display.GetRawText()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static void ValidateWindow(ProductionWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        if (window.FromUtc >= window.ToUtc)
        {
            throw new ArgumentException(
                "The planning window must satisfy FromUtc < ToUtc (half-open [FromUtc, ToUtc)).", nameof(window));
        }
        if (window.ToUtc - window.FromUtc > TimeSpan.FromDays(MaxWindowDays))
        {
            throw new ArgumentException(
                $"The planning window may span at most {MaxWindowDays} days.", nameof(window));
        }
    }
}
