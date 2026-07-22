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
    Task<Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>> ListMyOrdersAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);

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

    public async Task<Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>> ListMyOrdersAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // One paged query over the party's checked-out carts joined to their durable charge
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
                (c, s) => new { s.OrderId, c.BoxSize, s.Currency, s.Total, s.CreatedAt });

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

        var orders = await _orders.ListAsync(
            new ListOrdersQuery(OrderIds: rows.Select(r => r.OrderId).ToList(), PageSize: rows.Count),
            cancellationToken);
        var byId = orders.Items.ToDictionary(o => o.Id);

        var results = new List<StorefrontOrderSummaryDto>(rows.Count);
        foreach (var row in rows)
        {
            if (!byId.TryGetValue(row.OrderId, out var order))
            {
                continue;   // summary outlived its order — serve the rest rather than 500
            }
            results.Add(new StorefrontOrderSummaryDto(
                order.Id, order.CreatedAt, order.Status, row.Currency, row.Total, row.BoxSize));
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
