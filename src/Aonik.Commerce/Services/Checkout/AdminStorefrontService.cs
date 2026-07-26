using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// The Spec 083/081 admin projections: tenant-wide storefront orders (with the
/// payment/fulfilment/buyer facts the spine's generic list cannot supply), the
/// full storefront order detail, the carts admin read (list + detail, tokens
/// never serialized — R10), and a party's storefront summary for the unified
/// customer view. Read-only throughout: the cart availability/price flags and
/// the boxMeta drift state are computed against current state and never
/// persisted — drift REPAIR stays the customer load path's job (Spec 068 §8).
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

internal sealed class AdminStorefrontService : IAdminStorefrontService
{
    private const string DeliveryFeeItemType = "DeliveryFee";

    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOrderService _orders;
    private readonly IStorefrontOrderService _storefrontOrders;
    private readonly Catalog.IProductPricingService _pricing;

    public AdminStorefrontService(
        CommerceDbContext dbContext,
        ITenantProvider tenantProvider,
        IOrderService orders,
        IStorefrontOrderService storefrontOrders,
        Catalog.IProductPricingService pricing)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _orders = orders;
        _storefrontOrders = storefrontOrders;
        _pricing = pricing;
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
                continue;   // summary outlived its order — serve the rest rather than 500
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

        var selections = await _dbContext.OrderBundleSelections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.OrderId == orderId)
            .Select(s => new StorefrontOrderSelectionDto(s.ProductVariantId, s.Quantity, s.Sku, s.PersonalisationSummary))
            .ToListAsync(cancellationToken);

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
        var pageRows = await carts
            .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt)
            .ThenBy(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(c => c.Items)
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
                c.UpdatedAt ?? c.CreatedAt);
        }).ToList();

        return new Contracts.Models.Catalog.PagedResult<AdminCartRowDto>(results, totalCount, page, pageSize);
    }

    public async Task<AdminCartDetailDto?> GetCartAsync(Guid cartId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var cart = await _dbContext.Carts.AsNoTracking()
            .Include(c => c.Items)
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
                var flags = state.LineFlags.GetValueOrDefault(i.Id);
                return new AdminCartLineDto(
                    i.Id, i.LineKind, i.NameSnapshot, i.Sku, i.Quantity, i.UnitPriceSnapshot,
                    i.PersonalisationSummary, flags.Unavailable, flags.PriceChanged);
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
            cart.UpdatedAt ?? cart.CreatedAt,
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

    private readonly record struct LineFlags(bool Unavailable, bool PriceChanged);

    private sealed record CartComputed(
        decimal Total,
        AdminCartBoxMetaDto? BoxMeta,
        IReadOnlyDictionary<Guid, LineFlags> LineFlags);

    private static List<CartItem> LiveLines(Cart cart)
        => cart.Items.Where(i => !i.IsDeleted).ToList();

    /// <summary>
    /// Computes, batched across the page, everything the row/detail shapes carry
    /// but nothing persists: per-line availability (the SAME predicate the box
    /// path enforces — missing variant/product, deactivation, a vanished add-on
    /// price, or cart-wide demand over available stock, where a missing level
    /// row means ZERO), add-on price drift against the snapshot, the boxMeta
    /// drift state, and the honest cart value — the recorded charge total once
    /// checked out, otherwise the box goods value (box price + personalisation
    /// + surcharges + add-ons; delivery/discount/tax are checkout-time facts).
    /// Frozen (non-open) carts skip the live checks: their snapshots are the
    /// recorded truth, not a live session to re-validate.
    /// </summary>
    private async Task<Dictionary<Guid, CartComputed>> ComputeCartStatesAsync(
        IReadOnlyList<Cart> carts, CancellationToken ct)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var liveByCart = carts.ToDictionary(c => c.Id, LiveLines);
        var openCarts = carts.Where(c => c.Status == CartStatuses.Open).Select(c => c.Id).ToHashSet();

        // Catalogue state for every referenced variant (open carts only — frozen
        // carts are not re-validated).
        var checkVariantIds = carts.Where(c => openCarts.Contains(c.Id))
            .SelectMany(c => liveByCart[c.Id])
            .Select(i => i.ProductVariantId)
            .Distinct()
            .ToList();
        var variants = await _dbContext.ProductVariants.AsNoTracking()
            .Where(v => v.TenantId == tenantId && checkVariantIds.Contains(v.Id))
            .Select(v => new { v.Id, v.ProductId, v.IsActive })
            .ToDictionaryAsync(v => v.Id, ct);
        var productIds = variants.Values.Select(v => v.ProductId).Distinct().ToList();
        var productStatus = await _dbContext.Products.AsNoTracking()
            .Where(p => p.TenantId == tenantId && productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Status })
            .ToDictionaryAsync(p => p.Id, p => p.Status, ct);
        var levels = await _dbContext.InventoryLevels.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProductVariantId != null && checkVariantIds.Contains(l.ProductVariantId.Value))
            .GroupBy(l => l.ProductVariantId!.Value)
            .Select(g => new { VariantId = g.Key, Available = g.Sum(l => l.OnHand - l.Reserved) })
            .ToDictionaryAsync(x => x.VariantId, x => x.Available, ct);

        // Current retail for add-on lines, one resolution per distinct
        // (variant, currency) pair rather than per line.
        var priceKeys = carts.Where(c => openCarts.Contains(c.Id))
            .SelectMany(c => liveByCart[c.Id]
                .Where(i => i.LineKind == CartLineKinds.AddOn)
                .Select(i => (i.ProductVariantId, c.Currency)))
            .Distinct()
            .ToList();
        var currentPrices = new Dictionary<(Guid, string), decimal?>();
        foreach (var (variantId, currency) in priceKeys)
        {
            currentPrices[(variantId, currency)] = await _pricing.ResolvePriceAsync(variantId, currency, null, ct);
        }

        // Plans for open box carts (value derivation) and recorded totals for
        // checked-out ones.
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
            var isOpen = openCarts.Contains(cart.Id);
            var demand = lines.GroupBy(i => i.ProductVariantId).ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

            var flags = new Dictionary<Guid, LineFlags>(lines.Count);
            foreach (var line in lines)
            {
                if (!isOpen)
                {
                    flags[line.Id] = new LineFlags(false, false);
                    continue;
                }

                var variant = variants.GetValueOrDefault(line.ProductVariantId);
                var unavailable = variant is null
                    || !variant.IsActive
                    || productStatus.GetValueOrDefault(variant.ProductId) != ProductStatuses.Active;

                decimal? current = null;
                if (line.LineKind == CartLineKinds.AddOn)
                {
                    current = currentPrices.GetValueOrDefault((line.ProductVariantId, cart.Currency));
                    // X2 — an add-on whose retail price vanished can never check out.
                    unavailable = unavailable || current is null;
                }
                if (!unavailable)
                {
                    // Missing level row = zero stock, exactly like the box path.
                    var available = levels.GetValueOrDefault(line.ProductVariantId, 0m);
                    unavailable = demand.GetValueOrDefault(line.ProductVariantId) > available;
                }

                var priceChanged = line.LineKind == CartLineKinds.AddOn
                    && current is not null
                    && current.Value != line.UnitPriceSnapshot;
                flags[line.Id] = new LineFlags(unavailable, priceChanged);
            }

            var drift = isOpen && flags.Values.Any(f => f.Unavailable || f.PriceChanged);

            decimal total;
            if (cart.OrderId is { } orderId && recordedTotals.TryGetValue(orderId, out var recorded))
            {
                total = recorded;   // the charge summary is the authoritative checked-out value
            }
            else if (cart.BoxBundleProductId is { } bundleId
                && cart.BoxSize is { } size
                && plans.TryGetValue(bundleId, out var plan))
            {
                // The quote's snapshot arithmetic (Spec 068 §7 / 071 §6): the box is
                // priced as a container, so BoxDish snapshots are deliberately zero.
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
    /// Fulfilment derived from the spine's REAL lifecycle values: Complete is the
    /// spine's terminal success state (converged on payment completion — there is
    /// no separate persisted fulfilment stage for storefront orders today), and
    /// Cancelled/Failed/Expired all mean the order will never be fulfilled. When
    /// a dedicated fulfilment lifecycle lands, this projection re-derives from it.
    /// </summary>
    private static string DeriveFulfilment(string orderStatus) => orderStatus switch
    {
        OrderStatusCodes.Complete => "Fulfilled",
        OrderStatusCodes.Cancelled or OrderStatusCodes.Failed or OrderStatusCodes.Expired => "Cancelled",
        _ => "Unfulfilled",
    };
}
