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
    private async Task<Original?> ReadOriginalAsync(Guid orderId, Guid partyId, CancellationToken cancellationToken)
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

        return new Original(source, purchased);
    }

    public async Task<BoxCartDto?> ReorderAsync(Guid orderId, Guid partyId, CancellationToken cancellationToken = default)
    {
        var original = await ReadOriginalAsync(orderId, partyId, cancellationToken);
        if (original is null) return null;
        return await BuildAsync(original, original.Purchased.Select(dish => new ReorderDishChoice(dish.Id, checked((int)dish.Quantity))).ToArray(), original.Source.BoxSize!.Value, partyId, cancellationToken);
    }

    public async Task<ReorderPreviewDto?> PreviewAsync(Guid orderId, Guid partyId, CancellationToken cancellationToken = default)
    {
        var original = await ReadOriginalAsync(orderId, partyId, cancellationToken);
        if (original is null) return null;
        var rows = new List<ReorderDishPreview>();
        foreach (var dish in original.Purchased)
        {
            var maximum = await boxes.ReorderMaximumAsync(original.Source.BoxBundleProductId!.Value, dish.ProductVariantId, cancellationToken);
            rows.Add(new(dish.Id, dish.NameSnapshot ?? dish.Sku, checked((int)dish.Quantity),
                dish.PersonalisationSummary, dish.IsSignatureSnapshot ?? false, maximum));
        }
        return new(orderId, rows);
    }

    public async Task<BoxCartDto?> ReorderSelectedAsync(Guid orderId, Guid partyId, IReadOnlyList<ReorderDishChoice> selections, CancellationToken cancellationToken = default)
    {
        var original = await ReadOriginalAsync(orderId, partyId, cancellationToken);
        if (original is null) return null;
        if (selections is null || selections.Count is < 1 or > 99 || selections.Any(x => x is null || x.Quantity is < 1 or > 99)
            || selections.Select(x => x.SelectionId).Distinct().Count() != selections.Count
            || selections.Any(x => !original.Purchased.Any(dish => dish.Id == x.SelectionId))
            || selections.Sum(x => (long)x.Quantity) > 99)
            throw new StorefrontValidationException("Choose between 1 and 99 purchased dishes for your new box.");
        return await BuildAsync(original, selections, Math.Max(6, selections.Sum(x => x.Quantity)), partyId, cancellationToken);
    }

    private Task<BoxCartDto> BuildAsync(Original original, IReadOnlyList<ReorderDishChoice> selections, int size, Guid partyId, CancellationToken ct)
    {
        var dishes = selections.Select(choice => {
            var dish = original.Purchased.Single(x => x.Id == choice.SelectionId);
            return new ReorderBoxLine(new AddBoxLineCommand(dish.ProductVariantId, choice.Quantity, ReadSelection(dish.PersonalisationJson)), dish.NameSnapshot ?? dish.Sku);
        }).ToList();
        return boxes.CreateFromOrderAsync(new CreateBoxCartCommand(original.Source.BoxBundleProductId!.Value, size, BuyerPartyId: partyId), dishes, ct);
    }

    private sealed record Original(Cart Source, List<OrderBundleSelection> Purchased);

    private static JsonElement? ReadSelection(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

internal sealed record ReorderBoxLine(AddBoxLineCommand Line, string Name);
