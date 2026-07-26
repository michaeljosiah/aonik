using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
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
/// customer view. Read-only throughout: the cart detail's availability/price
/// flags are computed against current state and never persisted — drift REPAIR
/// stays the customer load path's job (Spec 068 §8).
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

        var results = pageRows.Select(c => new AdminCartRowDto(
            c.Id,
            c.BuyerPartyId is null ? "guest" : "party",
            c.BuyerPartyId,
            c.Status,
            c.Currency,
            c.Items.Sum(i => i.Quantity),
            c.Items.Sum(i => i.UnitPriceSnapshot * i.Quantity),
            BoxMeta(c),
            c.OrderId,
            c.UpdatedAt ?? c.CreatedAt)).ToList();

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

        // Read-only computed states. Availability: cart-wide demand per variant vs
        // current available stock (OnHand − Reserved) — the same aggregate rule the
        // box path enforces, evaluated without persisting anything. Price drift:
        // add-on snapshots vs the current retail price.
        var variantIds = cart.Items.Select(i => i.ProductVariantId).Distinct().ToList();
        var levels = await _dbContext.InventoryLevels.AsNoTracking()
            .Where(l => l.TenantId == tenantId && l.ProductVariantId != null && variantIds.Contains(l.ProductVariantId.Value))
            .GroupBy(l => l.ProductVariantId!.Value)
            .Select(g => new { VariantId = g.Key, Available = g.Sum(l => l.OnHand - l.Reserved) })
            .ToDictionaryAsync(x => x.VariantId, x => x.Available, cancellationToken);
        var demand = cart.Items
            .GroupBy(i => i.ProductVariantId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        var lines = new List<AdminCartLineDto>(cart.Items.Count);
        foreach (var i in cart.Items.OrderBy(i => i.CreatedAt))
        {
            var unavailable = cart.BoxBundleProductId is not null
                && levels.TryGetValue(i.ProductVariantId, out var available)
                && demand.TryGetValue(i.ProductVariantId, out var demanded)
                && demanded > available;

            var priceChanged = false;
            if (i.LineKind == CartLineKinds.AddOn)
            {
                var current = await _pricing.ResolvePriceAsync(i.ProductVariantId, cart.Currency, null, cancellationToken);
                priceChanged = current is null || current.Value != i.UnitPriceSnapshot;
            }

            lines.Add(new AdminCartLineDto(
                i.Id, i.LineKind, i.NameSnapshot, i.Sku, i.Quantity, i.UnitPriceSnapshot,
                i.PersonalisationSummary, unavailable, priceChanged));
        }

        return new AdminCartDetailDto(
            cart.Id,
            cart.BuyerPartyId is null ? "guest" : "party",
            cart.BuyerPartyId,
            cart.Status,
            cart.Currency,
            BoxMeta(cart),
            cart.OrderId,
            cart.UpdatedAt ?? cart.CreatedAt,
            lines);
    }

    public async Task<AdminPartyStorefrontDto> GetPartyStorefrontAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Reuse the Spec 072 party-scoped history verbatim — the admin view must
        // show exactly what the customer sees in their own account.
        var history = await _storefrontOrders.ListMyOrdersAsync(partyId, 1, 50, cancellationToken);

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
            history.Items,
            activeCart is null || activeCart.BoxSize is null
                ? null
                : new AdminPartyActiveCartDto(
                    activeCart.Id,
                    activeCart.BoxSize.Value,
                    (int)activeCart.Items.Where(i => i.LineKind == CartLineKinds.BoxDish).Sum(i => i.Quantity)),
            adopted);
    }

    private static AdminCartBoxMetaDto? BoxMeta(Cart cart)
        => cart.BoxBundleProductId is null || cart.BoxSize is null
            ? null
            : new AdminCartBoxMetaDto(
                cart.BoxSize.Value,
                (int)cart.Items.Where(i => i.LineKind == CartLineKinds.BoxDish).Sum(i => i.Quantity));

    private static string DeriveFulfilment(string orderStatus) => orderStatus switch
    {
        "Fulfilled" => "Fulfilled",
        "Cancelled" => "Cancelled",
        _ => "Unfulfilled",
    };
}
