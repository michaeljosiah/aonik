using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// The Spec 083/081 admin projections: tenant-wide storefront orders (with the
/// payment/fulfilment/buyer facts the spine's generic list cannot supply), the
/// full storefront order detail, the carts admin read (list + detail, tokens
/// never serialized — R10), and a party's storefront summary for the unified
/// customer view. Read-only throughout: the cart availability/price flags, the
/// per-line selection drift and the boxMeta drift state are computed against
/// current state and never persisted — drift REPAIR stays the customer load
/// path's job (Spec 068 §8).
/// </summary>
public interface IAdminStorefrontService
{
    Task<Contracts.Models.Catalog.PagedResult<AdminStorefrontOrderRowDto>> ListOrdersAsync(
        string? paymentStatus = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    Task<AdminOrderStorefrontDto?> GetOrderStorefrontAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task<Contracts.Models.Catalog.PagedResult<AdminCartRowDto>> ListCartsAsync(
        string? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    Task<AdminCartDetailDto?> GetCartAsync(Guid cartId, CancellationToken cancellationToken = default);

    Task<AdminPartyStorefrontDto> GetPartyStorefrontAsync(Guid partyId, CancellationToken cancellationToken = default);
}

internal sealed partial class AdminStorefrontService : IAdminStorefrontService
{
    private const string DeliveryFeeItemType = "DeliveryFee";

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOrderService _orders;
    private readonly IStorefrontOrderService _storefrontOrders;
    private readonly Catalog.IProductOptionService _options;
    private readonly Catalog.IOptionSelectionService _selections;
    private readonly Catalog.IProductPricingService _pricing;
    private readonly ILogger<AdminStorefrontService> _logger;

    public AdminStorefrontService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        IOrderService orders,
        IStorefrontOrderService storefrontOrders,
        Catalog.IProductOptionService options,
        Catalog.IOptionSelectionService selections,
        Catalog.IProductPricingService pricing,
        ILogger<AdminStorefrontService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _orders = orders;
        _storefrontOrders = storefrontOrders;
        _options = options;
        _selections = selections;
        _pricing = pricing;
        _logger = logger;
    }

    public async Task<Contracts.Models.Catalog.PagedResult<AdminStorefrontOrderRowDto>> ListOrdersAsync(
        string? paymentStatus = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Same join shape as the customer-facing history (Spec 072), tenant-wide:
        // checked-out carts are Commerce's ownership record; the durable charge
        // summary carries the funding facts the spine list cannot.
        var joined = _dbContext.Carts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.OrderId != null)
            .Join(
                _dbContext.OrderChargeSummaries.AsNoTracking().Where(s => s.TenantId == tenantId),
                c => c.OrderId!.Value,
                s => s.OrderId,
                (c, s) => new
                {
                    s.OrderId,
                    c.BuyerPartyId,
                    c.BoxSize,
                    s.Currency,
                    s.Total,
                    s.PaymentStatus,
                    s.CreatedAt,
                });
        if (!string.IsNullOrWhiteSpace(paymentStatus))
        {
            joined = joined.Where(x => x.PaymentStatus == paymentStatus);
        }

        var totalCount = await joined.CountAsync(cancellationToken);

        // Pagination is by SOURCE position — stable, so no order can duplicate
        // across pages or be skipped between them. Spine existence is not
        // filterable here (the orders live in another module behind a contract),
        // so a charge summary whose order has vanished cannot be excluded from
        // the count or the offset; pulling later rows forward to fill the gap
        // would shift every subsequent page boundary and duplicate rows. Such a
        // row is a data-integrity anomaly: it is LOGGED as a warning — the
        // operator needs to know a summary outlived its order — and omitted
        // from its page rather than invented.
        var rows = await joined
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new Contracts.Models.Catalog.PagedResult<AdminStorefrontOrderRowDto>([], totalCount, page, pageSize);
        }

        var spine = await _orders.ListAsync(
            new ListOrdersQuery(OrderIds: rows.Select(r => r.OrderId).ToList(), PageSize: rows.Count),
            cancellationToken);
        var byId = spine.Items.ToDictionary(o => o.Id);

        var results = new List<AdminStorefrontOrderRowDto>(rows.Count);
        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.OrderId, out var order))
            {
                LogOrphanedChargeSummary(_logger, row.OrderId, tenantId);
                continue;
            }
            results.Add(new AdminStorefrontOrderRowDto(
                order.Id,
                row.BuyerPartyId is null ? "guest" : "party",
                row.BuyerPartyId,
                order.CreatedAt,
                order.Status,
                row.PaymentStatus,
                DeriveFulfilment(order.Status),
                row.Currency,
                row.Total,
                row.BoxSize));
        }

        return new Contracts.Models.Catalog.PagedResult<AdminStorefrontOrderRowDto>(results, totalCount, page, pageSize);
    }

    public async Task<AdminOrderStorefrontDto?> GetOrderStorefrontAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var cart = await _dbContext.Carts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == orderId, cancellationToken);
        var summary = await _dbContext.OrderChargeSummaries.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.OrderId == orderId, cancellationToken);
        if (cart is null || summary is null)
        {
            return null;   // not a storefront order this tenant owns
        }

        var order = await _orders.GetAsync(orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        // Resolve display names for the items: the box aggregate names its bundle
        // product; add-ons name their variant. A miss falls back to the SKU —
        // display only, never invented data.
        var productIds = order.Items.Where(i => i.ProductId is not null).Select(i => i.ProductId!.Value).Distinct().ToList();
        var productNames = await _dbContext.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);
        var variantNames = await _dbContext.ProductVariants.AsNoTracking()
            .Where(v => v.TenantId == tenantId && productIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Name })
            .ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);

        var items = order.Items
            .OrderBy(i => i.ItemIndex)
            .Select(i =>
            {
                var isDelivery = string.Equals(i.ItemType, DeliveryFeeItemType, StringComparison.Ordinal);
                var isBoxAggregate = cart.BoxBundleProductId is not null
                    && i.ProductId == cart.BoxBundleProductId
                    && !isDelivery;
                var isAddOn = cart.BoxBundleProductId is not null && !isDelivery && !isBoxAggregate;
                var name = isDelivery
                    ? "Delivery"
                    : (i.ProductId is { } pid
                        ? (productNames.TryGetValue(pid, out var pn) ? pn
                            : variantNames.TryGetValue(pid, out var vn) ? vn : i.Sku ?? "Item")
                        : i.Sku ?? "Item");
                return new AdminOrderStorefrontItemDto(
                    i.ItemType, name, i.Sku, i.Quantity, i.UnitPrice, i.AmountIn, isAddOn, isDelivery);
            })
            .ToList();

        // Selections carry their OrderItemIndex so the drawer can nest each one
        // under its own bundle aggregate (an order may hold several), plus the
        // resolved variant name the drawer renders — SKU stays the durable
        // identifier, and a retired variant simply has no name rather than an
        // invented one.
        var selectionRows = await _dbContext.OrderBundleSelections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.OrderId == orderId)
            .OrderBy(s => s.OrderItemIndex).ThenBy(s => s.Sku)
            .ToListAsync(cancellationToken);
        var selectionVariantIds = selectionRows.Select(s => s.ProductVariantId).Distinct().ToList();
        var selectionNames = await _dbContext.ProductVariants.AsNoTracking()
            .Where(v => v.TenantId == tenantId && selectionVariantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.Name })
            .ToDictionaryAsync(v => v.Id, v => v.Name, cancellationToken);
        var selections = selectionRows
            .Select(s => new StorefrontOrderSelectionDto(
                s.ProductVariantId, s.Quantity, s.Sku, s.PersonalisationSummary,
                s.OrderItemIndex, selectionNames.GetValueOrDefault(s.ProductVariantId)))
            .ToList();

        return new AdminOrderStorefrontDto(
            order.Id,
            cart.BuyerPartyId is null ? "guest" : "party",
            cart.BuyerPartyId,
            order.CreatedAt,
            order.Status,
            summary.PaymentStatus,
            DeriveFulfilment(order.Status),
            items,
            selections,
            new AdminOrderChargeDto(
                summary.Subtotal, summary.DiscountTotal, summary.DiscountCode,
                summary.TaxTotal, summary.Total, summary.Currency),
            cart.BoxSize);
    }

    public async Task<Contracts.Models.Catalog.PagedResult<AdminCartRowDto>> ListCartsAsync(
        string? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var carts = _dbContext.Carts.AsNoTracking()
            .Where(c => c.TenantId == tenantId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            carts = carts.Where(c => c.Status == status);
        }

        var totalCount = await carts.CountAsync(cancellationToken);
        // Activity must reflect LINE writes too. Adds/removes stamp the parent
        // row (CartService.TouchCartAsync) — necessarily so for removals, which
        // the global soft-delete query filter hides from every per-line
        // aggregate — and the line-level Max keeps historical rows written
        // before that stamp existed ordering correctly.
        var pageRows = await carts
            .OrderByDescending(c =>
                c.Items.Max(i => (DateTime?)(i.UpdatedAt ?? i.CreatedAt)) > (c.UpdatedAt ?? c.CreatedAt)
                    ? c.Items.Max(i => (DateTime?)(i.UpdatedAt ?? i.CreatedAt))
                    : (c.UpdatedAt ?? c.CreatedAt))
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Items)
            .ThenInclude(i => i.Selections)
            .ToListAsync(cancellationToken);

        var computed = await ComputeCartStatesAsync(pageRows, cancellationToken);

        var results = pageRows.Select(c =>
        {
            var state = computed[c.Id];
            var lines = LiveLines(c);
            return new AdminCartRowDto(
                c.Id,
                c.BuyerPartyId is null ? "guest" : "party",
                c.BuyerPartyId,
                c.Status,
                c.Currency,
                lines.Sum(i => i.Quantity),
                state.Total,
                state.BoxMeta,
                c.OrderId,
                ActivityOf(c));
        }).ToList();

        return new Contracts.Models.Catalog.PagedResult<AdminCartRowDto>(results, totalCount, page, pageSize);
    }

    public async Task<AdminCartDetailDto?> GetCartAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts.AsNoTracking()
            .Include(c => c.Items)
            .ThenInclude(i => i.Selections)
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == cartId, cancellationToken);
        if (cart is null)
        {
            return null;
        }

        var state = (await ComputeCartStatesAsync([cart], cancellationToken))[cart.Id];

        var lines = LiveLines(cart)
            .OrderBy(i => i.CreatedAt)
            .Select(i =>
            {
                var flags = state.LineFlags.GetValueOrDefault(i.Id, LineFlags.Clean);
                return new AdminCartLineDto(
                    i.Id, i.LineKind, i.NameSnapshot, i.Sku, i.Quantity, i.UnitPriceSnapshot,
                    i.PersonalisationSummary,
                    string.IsNullOrWhiteSpace(i.PersonalisationJson) ? null : i.PersonalisationJson,
                    flags.Unavailable, flags.PriceChanged,
                    flags.SelectionDrift,
                    flags.Components);
            })
            .ToList();

        return new AdminCartDetailDto(
            cart.Id,
            cart.BuyerPartyId is null ? "guest" : "party",
            cart.BuyerPartyId,
            cart.Status,
            cart.Currency,
            state.BoxMeta,
            cart.OrderId,
            ActivityOf(cart),
            lines);
    }

    public async Task<AdminPartyStorefrontDto> GetPartyStorefrontAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Reuse the Spec 072 party-scoped history verbatim — the admin view must
        // show exactly what the customer sees in their own account — and walk
        // every page so the history is COMPLETE: a silent newest-N cap would
        // understate the party's storefront value without any indication.
        const int historyPageSize = 100;   // the service clamp's upper bound
        var firstPage = await _storefrontOrders.ListMyOrdersAsync(partyId, 1, historyPageSize, cancellationToken);
        var orders = new List<StorefrontOrderSummaryDto>(firstPage.Items);
        var totalPages = (int)Math.Ceiling(firstPage.TotalCount / (double)historyPageSize);
        for (var p = 2; p <= totalPages; p++)
        {
            var next = await _storefrontOrders.ListMyOrdersAsync(partyId, p, historyPageSize, cancellationToken);
            orders.AddRange(next.Items);
        }

        var activeCart = await _dbContext.Carts.AsNoTracking()
            .Include(c => c.Items)
            .Where(c => c.TenantId == tenantId
                && c.BuyerPartyId == partyId
                && c.Status == CartStatuses.Open
                && c.OrderId == null
                && c.BoxBundleProductId != null)
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // The RECORDED adoption fact: a party-bound cart whose guest token was
        // retired (AdoptAsync nulls it; a born-bound cart keeps its minted token).
        var adopted = await _dbContext.Carts.AsNoTracking()
            .AnyAsync(c => c.TenantId == tenantId && c.BuyerPartyId == partyId && c.AnonymousToken == null, cancellationToken);

        return new AdminPartyStorefrontDto(
            orders,
            activeCart is null || activeCart.BoxSize is null
                ? null
                : new AdminPartyActiveCartDto(
                    activeCart.Id,
                    activeCart.BoxSize.Value,
                    (int)LiveLines(activeCart).Where(i => i.LineKind == CartLineKinds.BoxDish).Sum(i => i.Quantity)),
            adopted);
    }

    // ─── Read-only computed state (Spec 083 dependency callout 2) ────────────

    private readonly record struct LineFlags(
        bool Unavailable,
        bool PriceChanged,
        IReadOnlyList<SelectionDrift> SelectionDrift,
        IReadOnlyList<AdminCartLineComponentDto> Components)
    {
        public static LineFlags Clean => new(false, false, [], []);
    }

    private sealed record CartComputed(
        decimal Total,
        AdminCartBoxMetaDto? BoxMeta,
        IReadOnlyDictionary<Guid, LineFlags> LineFlags);

    private static List<CartItem> LiveLines(Cart cart)
        => cart.Items.Where(i => !i.IsDeleted).ToList();

    /// <summary>Latest write across the cart row AND its loaded lines. Removals
    /// surface through the parent stamp (CartService.TouchCartAsync) because the
    /// soft-delete query filter hides deleted rows from the navigation.</summary>
    private static DateTime ActivityOf(Cart cart)
    {
        var own = cart.UpdatedAt ?? cart.CreatedAt;
        if (cart.Items.Count == 0)
        {
            return own;
        }
        var latestLine = cart.Items.Max(i => i.UpdatedAt ?? i.CreatedAt);
        return latestLine > own ? latestLine : own;
    }

    /// <summary>A cart is a LIVE editable session only while Open with no order
    /// claim — the same predicate the box path enforces. A pending-payment cart
    /// (Open but OrderId stamped) is frozen: its charge is fixed, its own
    /// inventory hold would read as self-inflicted unavailability, and its
    /// snapshots are the recorded truth.</summary>
    private static bool IsEditable(Cart cart)
        => cart.Status == CartStatuses.Open && cart.OrderId is null;

    /// <summary>
    /// Computes, batched across the page, everything the row/detail shapes carry
    /// but nothing persists: per-line availability (the SAME predicate the box
    /// path enforces — missing variant/product, deactivation, a vanished add-on
    /// price, or cart-wide demand over available DEFAULT-location stock, where a
    /// missing level row means ZERO), add-on retail drift, per-line selection
    /// renormalisation through the SAME Spec 066 drift rules (option retired /
    /// group added / price or surcharge moved), the boxMeta drift state, and the
    /// honest cart value — the recorded charge total once an order claimed the
    /// cart, otherwise the box goods value (box price + personalisation +
    /// surcharges + add-ons; delivery/discount/tax are checkout-time facts).
    /// Non-editable carts skip the live checks entirely.
    /// </summary>
    private async Task<Dictionary<Guid, CartComputed>> ComputeCartStatesAsync(
        IReadOnlyList<Cart> carts, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var liveByCart = carts.ToDictionary(c => c.Id, LiveLines);
        var editableCarts = carts.Where(IsEditable).Select(c => c.Id).ToHashSet();

        // Catalogue state for every referenced variant (editable carts only).
        // A classic BUNDLE line's own ProductVariantId is the bundle PRODUCT id
        // (Spec 042 carts) — its inventory-bearing variants are the component
        // selections, so those are what the availability predicate checks.
        var editableLines = carts.Where(c => editableCarts.Contains(c.Id))
            .SelectMany(c => liveByCart[c.Id])
            .ToList();
        var checkVariantIds = editableLines
            .SelectMany(i => i.IsBundle
                ? i.Selections.Where(sel => !sel.IsDeleted).Select(sel => sel.ProductVariantId)
                : new[] { i.ProductVariantId })
            .Distinct()
            .ToList();
        var variants = await _dbContext.ProductVariants.AsNoTracking()
            .Where(v => v.TenantId == tenantId && checkVariantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.IsActive })
            .ToDictionaryAsync(v => v.Id, ct);
        var bundleProductIds = editableLines
            .Where(i => i.IsBundle)
            .Select(i => i.BundleProductId ?? i.ProductVariantId)
            .Distinct()
            .ToList();
        // A box cart's CONTAINER product is referenced by the cart, not by any
        // line, so it must be loaded explicitly — the container's own sellability
        // is a blocker the per-line pass cannot see.
        var containerProductIds = carts
            .Where(c => editableCarts.Contains(c.Id) && c.BoxBundleProductId is not null)
            .Select(c => c.BoxBundleProductId!.Value)
            .Distinct()
            .ToList();
        var productIds = variants.Values.Select(v => v.ProductId)
            .Union(bundleProductIds)
            .Union(containerProductIds)
            .Distinct()
            .ToList();
        var products = await _dbContext.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Kind, p.Status, p.BundlePricingMode, p.UnitSurcharge, p.UnitSurchargeCurrency })
            .ToDictionaryAsync(p => p.Id, ct);
        // Availability reads the DEFAULT stock row (Location == null) — the same
        // row the checkout path's InventoryService consults; summing location
        // rows would overstate what the add/checkout guard actually sees.
        var levels = await _dbContext.InventoryLevels.AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && l.Location == null
                && l.ProductVariantId != null
                && checkVariantIds.Contains(l.ProductVariantId.Value))
            .GroupBy(l => l.ProductVariantId!.Value)
            .Select(g => new { VariantId = g.Key, Available = g.Sum(l => l.OnHand - l.Reserved) })
            .ToDictionaryAsync(x => x.VariantId, x => x.Available, ct);

        // Current retail for add-on lines: ONE bounded query over the page's
        // (variant, currency) keys, resolved to the latest effective active row
        // in memory — the same rule as ResolvePriceAsync, without a round trip
        // per key.
        var priceKeys = carts.Where(c => editableCarts.Contains(c.Id))
            .SelectMany(c => liveByCart[c.Id]
                .Where(i => i.LineKind == CartLineKinds.AddOn)
                .Select(i => (i.ProductVariantId, c.Currency)))
            .Distinct()
            .ToList();
        var currentPrices = new Dictionary<(Guid, string), decimal?>();
        foreach (var currencyGroup in priceKeys.GroupBy(k => k.Currency))
        {
            var resolved = await _pricing.ResolvePricesAsync(
                currencyGroup.Select(k => k.ProductVariantId).ToList(), currencyGroup.Key, null, ct);
            foreach (var key in currencyGroup)
            {
                currentPrices[key] = resolved.GetValueOrDefault(key.ProductVariantId);
            }
        }

        // Effective option groups for every product an editable box-cart line
        // references — the personalisation half of drift runs through the SAME
        // Spec 066 rules, batched (constant queries per page).
        var boxLineProductIds = carts
            .Where(c => editableCarts.Contains(c.Id) && c.BoxBundleProductId is not null)
            .SelectMany(c => liveByCart[c.Id])
            .Select(i => variants.GetValueOrDefault(i.ProductVariantId)?.ProductId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var effectiveGroups = await _options.GetEffectiveOptionsBatchAsync(boxLineProductIds, ct);

        // Plans for unclaimed box carts (value derivation) and recorded totals
        // for claimed ones.
        var planProductIds = carts
            .Where(c => c.BoxBundleProductId is not null && c.OrderId is null)
            .Select(c => c.BoxBundleProductId!.Value)
            .Distinct()
            .ToList();
        var plans = await _dbContext.BundleSizePlans.AsNoTracking()
            .Include(p => p.Presets)
            .Where(p => p.TenantId == tenantId && planProductIds.Contains(p.BundleProductId) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.BundleProductId, ct);
        var orderIds = carts.Where(c => c.OrderId is not null).Select(c => c.OrderId!.Value).Distinct().ToList();
        var recordedTotals = await _dbContext.OrderChargeSummaries.AsNoTracking()
            .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
            .ToDictionaryAsync(s => s.OrderId, s => s.Total, ct);

        var result = new Dictionary<Guid, CartComputed>(carts.Count);
        foreach (var cart in carts)
        {
            var lines = liveByCart[cart.Id];
            var isEditable = editableCarts.Contains(cart.Id);
            var isBox = cart.BoxBundleProductId is not null;
            // Demand per inventory-bearing variant: a bundle line draws its
            // COMPONENT variants' stock (line qty x selection qty); everything
            // else draws its own.
            var demand = lines
                .SelectMany(i => i.IsBundle
                    ? i.Selections.Where(sel => !sel.IsDeleted)
                        .Select(sel => (VariantId: sel.ProductVariantId, Quantity: i.Quantity * sel.Quantity))
                    : new[] { (VariantId: i.ProductVariantId, Quantity: i.Quantity) })
                .GroupBy(x => x.VariantId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

            var flags = new Dictionary<Guid, LineFlags>(lines.Count);
            foreach (var line in lines)
            {
                if (!isEditable)
                {
                    flags[line.Id] = LineFlags.Clean;
                    continue;
                }

                // Classic bundle line: the line id is the bundle PRODUCT; the
                // availability verdict resolves through the component selections,
                // each carrying its own flag for the drawer.
                if (line.IsBundle)
                {
                    var bundleProduct = products.GetValueOrDefault(line.BundleProductId ?? line.ProductVariantId);
                    var components = line.Selections.Where(sel => !sel.IsDeleted)
                        .Select(sel =>
                        {
                            var compVariant = variants.GetValueOrDefault(sel.ProductVariantId);
                            var compProduct = compVariant is null ? null : products.GetValueOrDefault(compVariant.ProductId);
                            var compUnavailable = compVariant is null
                                || !compVariant.IsActive
                                || compProduct is null
                                || compProduct.Status != ProductStatuses.Active
                                || demand.GetValueOrDefault(sel.ProductVariantId) > levels.GetValueOrDefault(sel.ProductVariantId, 0m);
                            return new AdminCartLineComponentDto(
                                sel.ProductVariantId, sel.Sku, sel.NameSnapshot, sel.Quantity, compUnavailable);
                        })
                        .ToList();
                    var bundleUnavailable = bundleProduct is null
                        || bundleProduct.Status != ProductStatuses.Active
                        || components.Any(comp => comp.IsUnavailable);
                    flags[line.Id] = new LineFlags(bundleUnavailable, false, [], components);
                    continue;
                }

                var variant = variants.GetValueOrDefault(line.ProductVariantId);
                var product = variant is null ? null : products.GetValueOrDefault(variant.ProductId);
                var unavailable = variant is null
                    || !variant.IsActive
                    || product is null
                    || product.Status != ProductStatuses.Active;

                decimal? currentRetail = null;
                if (line.LineKind == CartLineKinds.AddOn)
                {
                    currentRetail = currentPrices.GetValueOrDefault((line.ProductVariantId, cart.Currency));
                    // X2 — an add-on whose retail price vanished can never check out.
                    unavailable = unavailable || currentRetail is null;
                }
                if (!unavailable)
                {
                    // Missing level row = zero stock, exactly like the box path.
                    var available = levels.GetValueOrDefault(line.ProductVariantId, 0m);
                    unavailable = demand.GetValueOrDefault(line.ProductVariantId) > available;
                }

                var priceChanged = line.LineKind == CartLineKinds.AddOn
                    && currentRetail is not null
                    && currentRetail.Value != line.UnitPriceSnapshot;

                // Personalisation drift — the same renormalisation the box load
                // path persists, run pure here: retired options remap, gained
                // groups default, and a moved option price or product surcharge
                // shows up as a repriced selection vs the stored snapshots.
                IReadOnlyList<SelectionDrift> selectionDrift = [];
                if (isBox && variant is not null && product is not null)
                {
                    // Unconditional for every box line — including an unpersonalised
                    // product with zero option groups. ApplyDriftAsync renormalises
                    // the equivalent empty selection on the customer path, so a
                    // product surcharge that later moves (or is re-denominated) must
                    // surface here too; skipping those lines reported a cart as clean
                    // that checkout would stop on for the changed charge.
                    var groups = effectiveGroups.GetValueOrDefault(variant.ProductId, []);
                    try
                    {
                        var renorm = _selections.RenormalizeStored(
                            groups, line.PersonalisationJson, cart.Currency,
                            product.UnitSurcharge, product.UnitSurchargeCurrency);
                        selectionDrift = renorm.Drift;
                        var repriced =
                            renorm.Result.Adjustment != (line.PersonalisationAdjustment ?? 0m)
                            || (renorm.Result.UnitSurcharge ?? 0m) != (line.UnitSurcharge ?? 0m);
                        priceChanged = priceChanged || repriced;
                    }
                    catch (Catalog.OptionValidationException)
                    {
                        // V10 — a group or surcharge re-denominated after the line
                        // was stored. The box path cannot price this either; it is
                        // a blocking drift state, not a 500.
                        unavailable = true;
                        selectionDrift = [new SelectionDrift(string.Empty, null, null, "currency-mismatch")];
                    }
                }

                flags[line.Id] = new LineFlags(unavailable, priceChanged, selectionDrift, []);
            }

            // The CONTAINER is a blocker in its own right, independently of every
            // line: PrepareForCheckoutAsync rejects the cart when the box product
            // stopped being a sellable size-tiered bundle (archived, moved back to
            // Draft, re-kinded, or re-priced off SizeTiered), and ValidateSize
            // rejects it when an operator narrowed MinSize/MaxSize past the size
            // this session chose. Either way the carts table would otherwise show
            // a healthy, resumable session that cannot check out.
            var containerBlocked = false;
            if (isEditable && isBox)
            {
                var containerId = cart.BoxBundleProductId!.Value;
                var container = products.GetValueOrDefault(containerId);
                containerBlocked = container is null
                    || container.Kind != ProductKinds.Bundle
                    || container.Status != ProductStatuses.Active
                    || container.BundlePricingMode != BundlePricingModes.SizeTiered
                    || cart.BoxSize is not { } currentSize
                    || !plans.TryGetValue(containerId, out var currentPlan)
                    || !BoxPricing.IsValidSize(currentPlan, currentSize);
            }

            var drift = isEditable && isBox
                && (containerBlocked
                    || flags.Values.Any(f => f.Unavailable || f.PriceChanged || f.SelectionDrift.Count > 0));

            decimal total;
            if (cart.OrderId is { } orderId && recordedTotals.TryGetValue(orderId, out var recorded))
            {
                total = recorded;   // the charge summary is the authoritative claimed-cart value
            }
            else if (cart.BoxBundleProductId is { } bundleId
                && cart.BoxSize is { } size
                && plans.TryGetValue(bundleId, out var plan))
            {
                // The quote's snapshot arithmetic (Spec 068 §7 / 071 §6): the box is
                // priced as a container, so BoxDish snapshots are deliberately zero.
                // A now-out-of-range size prices by formula extrapolation — the
                // operator still needs a figure, and `drift` already reports that
                // this session cannot continue at that size.
                total = BoxPricing.BoxPrice(plan, size)
                    + lines.Where(l => l.LineKind == CartLineKinds.BoxDish)
                        .Sum(l => ((l.PersonalisationAdjustment ?? 0m) + (l.UnitSurcharge ?? 0m)) * l.Quantity)
                    + lines.Where(l => l.LineKind == CartLineKinds.AddOn)
                        .Sum(l => (l.UnitPriceSnapshot + (l.PersonalisationAdjustment ?? 0m) + (l.UnitSurcharge ?? 0m)) * l.Quantity);
            }
            else
            {
                total = lines.Sum(l => (l.UnitPriceSnapshot + (l.PersonalisationAdjustment ?? 0m) + (l.UnitSurcharge ?? 0m)) * l.Quantity);
            }

            var boxMeta = cart.BoxBundleProductId is null || cart.BoxSize is null
                ? null
                : new AdminCartBoxMetaDto(
                    cart.BoxSize.Value,
                    (int)lines.Where(i => i.LineKind == CartLineKinds.BoxDish).Sum(i => i.Quantity),
                    drift);

            result[cart.Id] = new CartComputed(total, boxMeta, flags);
        }

        return result;
    }

    /// <summary>
    /// Fulfilment derived from the spine's REAL lifecycle values — honestly.
    /// No storefront fulfilment stage is persisted today, and the spine's
    /// Complete is converged by PAYMENT completion, which is not evidence of
    /// delivery — so nothing maps to Fulfilled yet: paid orders stay visible in
    /// the awaiting-fulfilment view until a real fulfilment lifecycle supplies
    /// the fact, and Cancelled/Failed/Expired all mean it never will be.
    /// </summary>
    private static string DeriveFulfilment(string orderStatus) => orderStatus switch
    {
        OrderStatusCodes.Cancelled or OrderStatusCodes.Failed or OrderStatusCodes.Expired => "Cancelled",
        _ => "Unfulfilled",
    };

    [LoggerMessage(EventId = 8301, Level = LogLevel.Warning,
        Message = "Storefront order list skipped charge summary for order {OrderId} (tenant {TenantId}): the spine order no longer exists.")]
    private static partial void LogOrphanedChargeSummary(ILogger logger, Guid orderId, Guid tenantId);
}
