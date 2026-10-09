using Aonik.Commerce.Persistence;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Fulfilment;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Spec 072 Y5 — a customer's own orders. Scoping is the QUERY (Z5): the party's carts
/// are the Commerce-owned record linking a customer to the orders checkout produced, so another
/// party's order id simply does not resolve — a 404, never a 403 oracle.</summary>
public interface IStorefrontOrderService
{
    Task<Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>> ListMyOrdersAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

    Task<StorefrontOrderDetailDto?> GetMyOrderAsync(Guid partyId, Guid orderId, CancellationToken cancellationToken = default);

    Task<StorefrontOrderDetailDto?> GetGuestOrderAsync(Guid orderId, string? token, CancellationToken cancellationToken = default);
}

public record StorefrontOrderSummaryDto(
    Guid OrderId,
    DateTime PlacedAtUtc,
    string Status,
    string Currency,
    decimal Total,
    int? BoxSize,
    DateOnly? DeliveryDate = null,
    bool IsGift = false,
    string? OrderNumber = null,
    string? PaymentStatus = null,
    string? DiscountCode = null,
    decimal DiscountTotal = 0m,
    IReadOnlyList<StorefrontOrderSelectionDto>? Selections = null,
    string? FulfilmentStatus = null,
    string? HistoryGroup = null);

public record StorefrontOrderItemDto(
    string ItemType,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal AmountIn,
    string? Sku,
    string? Name = null,
    int ItemIndex = 0);

public record StorefrontOrderSelectionDto(
    Guid ProductVariantId,
    decimal Quantity,
    string Sku,
    string? PersonalisationSummary,
    /// Which order ITEM this selection sits under — an order may carry several
    /// bundle aggregates, and a flat list cannot say which is whose.
    int OrderItemIndex = 0,
    /// Purchased name, never refreshed from today's catalogue; null for legacy snapshots.
    string? Name = null,
    bool? IsSignature = null);

public record StorefrontOrderDetailDto(
    Guid OrderId,
    DateTime PlacedAtUtc,
    string Status,
    string Currency,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal TaxTotal,
    decimal Total,
    int? BoxSize,
    IReadOnlyList<StorefrontOrderItemDto> Items,
    IReadOnlyList<StorefrontOrderSelectionDto> Selections,
    string PaymentStatus,
    OrderDeliveryDto? Delivery = null,
    string? OrderNumber = null,
    string? DiscountCode = null,
    string? FulfilmentStatus = null);

internal sealed class StorefrontOrderService : IStorefrontOrderService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOrderService _orders;
    private readonly GuestOrderAccess _guestOrders;

    public StorefrontOrderService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IOrderService orders, GuestOrderAccess guestOrders)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _orders = orders;
        _guestOrders = guestOrders;
    }

    public async Task<Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>> ListMyOrdersAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // One paged query over the party's checkout attempts joined to their durable charge
        // summaries (an order created then unwound — the K4 path — has no summary and drops out
        // of the join). The page is fixed HERE, so an established customer's history never
        // becomes an unbounded read; the ordering layer is then asked for exactly that page's
        // orders in ONE batched query, never per-order round trips.
        var joined = _dbContext.Carts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.BuyerPartyId == partyId && c.OrderId != null)
            .Join(
                _dbContext.OrderChargeSummaries.AsNoTracking().Where(s => s.TenantId == tenantId),
                c => c.OrderId!.Value,
                s => s.OrderId,
                (c, s) => new { s.OrderId, c.BoxSize, s.Currency, s.Total, s.CreatedAt,
                    s.PaymentStatus, s.DiscountCode, s.DiscountTotal });

        var totalCount = await joined.CountAsync(cancellationToken);
        var rows = await joined
            .OrderByDescending(x => x.CreatedAt)
            .ThenBy(x => x.OrderId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return new Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>([], totalCount, page, pageSize);
        }

        var orderIds = rows.Select(r => r.OrderId).ToList();
        var deliveryDates = await _dbContext.OrderDeliveryDetails.AsNoTracking()
            .Where(d => d.TenantId == tenantId && orderIds.Contains(d.OrderId))
            .Select(d => new { d.OrderId, d.DeliveryDate, d.IsGift, d.FulfilmentStatus })
            .ToDictionaryAsync(d => d.OrderId, cancellationToken);
        var orders = await _orders.ListAsync(
            new ListOrdersQuery(OrderIds: orderIds, PageSize: rows.Count),
            cancellationToken);
        var byId = orders.Items.ToDictionary(o => o.Id);
        var selectionRows = await _dbContext.OrderBundleSelections.AsNoTracking()
            .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
            .OrderBy(s => s.OrderItemIndex).ThenBy(s => s.Id)
            .Select(s => new { s.OrderId, Selection = new StorefrontOrderSelectionDto(
                s.ProductVariantId, s.Quantity, s.Sku, s.PersonalisationSummary, s.OrderItemIndex,
                s.NameSnapshot, s.IsSignatureSnapshot) })
            .ToListAsync(cancellationToken);
        var selectionsByOrder = selectionRows.GroupBy(s => s.OrderId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<StorefrontOrderSelectionDto>)group.Select(s => s.Selection).ToList());

        var results = new List<StorefrontOrderSummaryDto>(rows.Count);
        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.OrderId, out var order))
            {
                continue;   // summary outlived its order — serve the rest rather than 500
            }
            results.Add(new StorefrontOrderSummaryDto(
                order.Id, order.CreatedAt, order.Status, row.Currency, row.Total, row.BoxSize,
                deliveryDates.GetValueOrDefault(row.OrderId)?.DeliveryDate,
                deliveryDates.GetValueOrDefault(row.OrderId)?.IsGift ?? false,
                order.OrderNumber, row.PaymentStatus, row.DiscountCode, row.DiscountTotal,
                selectionsByOrder.GetValueOrDefault(row.OrderId) ?? [],
                OrderFulfilmentData.Status(deliveryDates.GetValueOrDefault(row.OrderId)?.FulfilmentStatus, row.PaymentStatus, order.Status),
                OrderFulfilmentData.HistoryGroup(deliveryDates.GetValueOrDefault(row.OrderId)?.FulfilmentStatus, row.PaymentStatus, order.Status)));
        }

        return new Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>(results, totalCount, page, pageSize);
    }

    public async Task<StorefrontOrderDetailDto?> GetMyOrderAsync(Guid partyId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Z5 — ownership is the query: no owning cart, no order. Another party's id is a 404.
        var cart = await _dbContext.Carts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.TenantId == tenantId && c.BuyerPartyId == partyId && c.OrderId == orderId,
                cancellationToken);
        return cart is null ? null : await GetDetailAsync(tenantId, orderId, cart.BoxSize, cancellationToken);
    }

    public async Task<StorefrontOrderDetailDto?> GetGuestOrderAsync(Guid orderId, string? token, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        if (!_guestOrders.IsValid(token, tenantId, orderId)) return null;

        var cart = await _dbContext.Carts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.OrderId == orderId, cancellationToken);
        return cart is null ? null : await GetDetailAsync(tenantId, orderId, cart.BoxSize, cancellationToken);
    }

    // Both callers authorize first; this projection contains no payment secrets or raw order metadata.
    private async Task<StorefrontOrderDetailDto?> GetDetailAsync(Guid tenantId, Guid orderId, int? boxSize, CancellationToken cancellationToken)
    {
        var order = await _orders.GetAsync(orderId, cancellationToken);
        var summary = await _dbContext.OrderChargeSummaries
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.OrderId == orderId, cancellationToken);
        if (order is null || summary is null)
        {
            return null;
        }

        var selections = await _dbContext.OrderBundleSelections
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.OrderId == orderId)
            .OrderBy(s => s.OrderItemIndex).ThenBy(s => s.Id)
            .Select(s => new StorefrontOrderSelectionDto(
                s.ProductVariantId, s.Quantity, s.Sku, s.PersonalisationSummary, s.OrderItemIndex,
                s.NameSnapshot, s.IsSignatureSnapshot))
            .ToListAsync(cancellationToken);

        var delivery = await _dbContext.OrderDeliveryDetails.AsNoTracking()
            .FirstOrDefaultAsync(d => d.TenantId == tenantId && d.OrderId == orderId, cancellationToken);

        return new StorefrontOrderDetailDto(
            order.Id,
            order.CreatedAt,
            order.Status,
            summary.Currency,
            summary.Subtotal,
            summary.DiscountTotal,
            summary.TaxTotal,
            summary.Total,
            boxSize,
            order.Items
                .OrderBy(i => i.ItemIndex)
                .Select(i => new StorefrontOrderItemDto(i.ItemType, i.Quantity, i.UnitPrice, i.AmountIn, i.Sku, i.NameSnapshot, i.ItemIndex))
                .ToList(),
            selections,
            summary.PaymentStatus,
            delivery is null ? null : OrderDeliveryMapper.Map(delivery), order.OrderNumber, summary.DiscountCode,
            OrderFulfilmentData.Status(delivery?.FulfilmentStatus, summary.PaymentStatus, order.Status));
    }
}
