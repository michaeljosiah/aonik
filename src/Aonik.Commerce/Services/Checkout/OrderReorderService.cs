using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class OrderReorderService(CommerceDbContext db, ITenantProvider tenantProvider,
    IOrderService orders, BoxCartService boxes) : IOrderReorderService
{
    public async Task<BoxCartDto?> ReorderAsync(Guid orderId, Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var source = await db.Carts.AsNoTracking().FirstOrDefaultAsync(cart => cart.TenantId == tenantId
            && cart.BuyerPartyId == partyId && cart.OrderId == orderId, cancellationToken);
        if (source is null) return null;
        var order = await orders.GetAsync(orderId, cancellationToken);
        if (order is null) return null;
        var payment = await db.OrderChargeSummaries.AsNoTracking()
            .Where(summary => summary.TenantId == tenantId && summary.OrderId == orderId)
            .Select(summary => summary.PaymentStatus).FirstOrDefaultAsync(cancellationToken);
        if (source.Status != CartStatuses.CheckedOut || payment != CheckoutPaymentStatuses.Captured
            || order.OrderType != OrderTypeCodes.ProductPurchase || source.BoxBundleProductId is not { } bundleId
            || source.BoxSize is not { } size)
            throw new StorefrontValidationException("Only a confirmed paid food box can be reordered.");

        var boxItems = order.Items.Where(item => item.ItemType == OrderTypeCodes.ProductPurchase && item.ProductId == bundleId)
            .Select(item => item.ItemIndex).ToArray();
        var purchased = await db.OrderBundleSelections.AsNoTracking()
            .Where(selection => selection.TenantId == tenantId && selection.OrderId == orderId
                && boxItems.Contains(selection.OrderItemIndex))
            .OrderBy(selection => selection.OrderItemIndex).ThenBy(selection => selection.Id)
            .ToListAsync(cancellationToken);
        if (purchased.Count == 0 || purchased.Any(dish => dish.Quantity <= 0 || dish.Quantity != decimal.Truncate(dish.Quantity))
            || purchased.Sum(dish => dish.Quantity) != size)
            throw new StorefrontValidationException("The purchased box contents are incomplete; start a new box instead.");

        var dishes = purchased.Select(dish => new ReorderBoxLine(
            new AddBoxLineCommand(dish.ProductVariantId, checked((int)dish.Quantity), ReadSelection(dish.PersonalisationJson)),
            dish.NameSnapshot ?? dish.Sku)).ToList();
        return await boxes.CreateFromOrderAsync(new CreateBoxCartCommand(bundleId, size, BuyerPartyId: partyId),
            dishes, cancellationToken);
    }

    private static JsonElement? ReadSelection(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal sealed record ReorderBoxLine(AddBoxLineCommand Line, string Name);
