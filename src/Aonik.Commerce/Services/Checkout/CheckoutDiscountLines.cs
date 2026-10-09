using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Ordering;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Maps already priced goods to catalogue identities; never prices or persists a line.</summary>
internal static class CheckoutDiscountLines
{
    public static async Task<IReadOnlyList<DiscountChargeLine>> FromOrderItemsAsync(CommerceDbContext db,
        Guid tenantId, IReadOnlyList<OrderItemCommand> items, CancellationToken cancellationToken = default)
    {
        // Stored-value, delivery and future non-goods item types are ineligible by default.
        var goods = items.Where(x => x.ItemType == OrderTypeCodes.ProductPurchase).ToList();
        var ids = goods.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.Id))
            .Join(db.Products.AsNoTracking().Where(x => x.TenantId == tenantId),
                variant => variant.ProductId, product => product.Id, (variant, product) => new { variant.Id, ProductId = product.Id })
            .ToDictionaryAsync(x => x.Id, x => x.ProductId, cancellationToken);
        var bundles = await db.Products.AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.Id) && x.Kind == ProductKinds.Bundle)
            .Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<DiscountChargeLine>(goods.Count);
        foreach (var item in goods)
        {
            if (item.ProductId is not { } reference)
                throw new DiscountException(DiscountException.NotEligible);
            var productId = variants.TryGetValue(reference, out var parent) ? parent
                : bundles.Contains(reference) ? reference : Guid.Empty;
            if (productId == Guid.Empty) throw new DiscountException(DiscountException.NotEligible);
            result.Add(new DiscountChargeLine(item.ItemIndex, productId, item.AmountIn));
        }
        return result;
    }
}
