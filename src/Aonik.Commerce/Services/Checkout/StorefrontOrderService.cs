using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Spec 072 Y5 — a customer's own orders. Scoping is the QUERY (Z5): the party's carts
/// are the Commerce-owned record linking a customer to the orders checkout produced, so another
/// party's order id simply does not resolve — a 404, never a 403 oracle.</summary>
public interface IStorefrontOrderService
{
    Task<IReadOnlyList<StorefrontOrderSummaryDto>> ListMyOrdersAsync(Guid partyId, CancellationToken cancellationToken = default);

    Task<StorefrontOrderDetailDto?> GetMyOrderAsync(Guid partyId, Guid orderId, CancellationToken cancellationToken = default);
}

public record StorefrontOrderSummaryDto(
    Guid OrderId,
    DateTime PlacedAtUtc,
    string Status,
    string Currency,
    decimal Total,
    int? BoxSize);

public record StorefrontOrderItemDto(
    string ItemType,
    decimal? Quantity,
    decimal? UnitPrice,
    decimal AmountIn,
    string? Sku);

public record StorefrontOrderSelectionDto(
    Guid ProductVariantId,
    decimal Quantity,
    string Sku,
    string? PersonalisationSummary);

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
    IReadOnlyList<StorefrontOrderSelectionDto> Selections);

internal sealed class StorefrontOrderService : IStorefrontOrderService
{
    private readonly CommerceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IOrderService _orders;

    public StorefrontOrderService(CommerceDbContext dbContext, ITenantProvider tenantProvider, IOrderService orders)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _orders = orders;
    }

    public async Task<IReadOnlyList<StorefrontOrderSummaryDto>> ListMyOrdersAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var carts = await _dbContext.Carts
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.BuyerPartyId == partyId && c.OrderId != null)
            .Select(c => new { OrderId = c.OrderId!.Value, c.BoxSize })
            .ToListAsync(cancellationToken);
        if (carts.Count == 0)
        {
            return [];
        }

        var orderIds = carts.Select(c => c.OrderId).ToList();
        var summaries = await _dbContext.OrderChargeSummaries
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && orderIds.Contains(s.OrderId))
            .ToDictionaryAsync(s => s.OrderId, cancellationToken);

        var results = new List<StorefrontOrderSummaryDto>();
        foreach (var cart in carts)
        {
            var order = await _orders.GetAsync(cart.OrderId, cancellationToken);
            if (order is null || !summaries.TryGetValue(cart.OrderId, out var summary))
            {
                continue;   // an order created then unwound (the K4 path) has no durable summary
            }
            results.Add(new StorefrontOrderSummaryDto(
                order.Id, order.CreatedAt, order.Status, summary.Currency, summary.Total, cart.BoxSize));
        }

        return results.OrderByDescending(r => r.PlacedAtUtc).ToList();
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
        if (cart is null)
        {
            return null;
        }

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
            .Select(s => new StorefrontOrderSelectionDto(
                s.ProductVariantId, s.Quantity, s.Sku, s.PersonalisationSummary))
            .ToListAsync(cancellationToken);

        return new StorefrontOrderDetailDto(
            order.Id,
            order.CreatedAt,
            order.Status,
            summary.Currency,
            summary.Subtotal,
            summary.DiscountTotal,
            summary.TaxTotal,
            summary.Total,
            cart.BoxSize,
            order.Items
                .Select(i => new StorefrontOrderItemDto(i.ItemType, i.Quantity, i.UnitPrice, i.AmountIn, i.Sku))
                .ToList(),
            selections);
    }
}
