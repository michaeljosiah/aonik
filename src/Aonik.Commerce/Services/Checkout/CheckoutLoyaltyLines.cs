using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Checkout;

internal static class CheckoutLoyaltyLines
{
    public const string GiftCardValueItemType = "GiftCardValue";

    public static async Task<IReadOnlyList<LoyaltyChargedLine>> ResolveAsync(CommerceDbContext db,
        Guid tenantId, IReadOnlyList<OrderItemCommand> items, DiscountComputation discount, LoyaltyPolicy policy,
        CancellationToken cancellationToken = default)
    {
        if (items.Select(item => item.ItemIndex).Distinct().Count() != items.Count)
            throw new StorefrontValidationException("Checkout line indexes must be unique.");
        var goods = items.Where(item => item.ItemType is OrderTypeCodes.ProductPurchase or GiftCardValueItemType)
            .Select(item => item with { ItemType = OrderTypeCodes.ProductPurchase }).ToList();
        // Reuse the tenant-scoped variant/bundle identity mapping, not coupon eligibility: a
        // greeting card can accept a voucher but must never earn or redeem loyalty points.
        var mapped = await CheckoutDiscountLines.FromOrderItemsAsync(db, tenantId, goods, cancellationToken);
        var products = mapped.ToDictionary(line => line.Index, line => line.ProductId);
        var allocations = discount.Allocations.ToDictionary(line => line.ItemIndex, line => line.Amount);
        if (allocations.Keys.Any(index => items.All(item => item.ItemIndex != index))
            || allocations.Values.Sum() != discount.Amount)
            throw new StorefrontValidationException("Coupon allocations do not match the checkout lines.");
        var earnDiscountExcluded = discount.DiscountId is { } earnDiscount && policy.EarnExcludedDiscountIds?.Contains(earnDiscount) == true;
        var redeemDiscountExcluded = discount.DiscountId is { } redeemDiscount && policy.RedeemExcludedDiscountIds?.Contains(redeemDiscount) == true;
        return items.OrderBy(item => item.ItemIndex).Select(item =>
        {
            var productId = products.GetValueOrDefault(item.ItemIndex);
            var earnKind = item.ItemType is OrderTypeCodes.ProductPurchase or GiftCardValueItemType;
            var redeemKind = item.ItemType == OrderTypeCodes.ProductPurchase;
            return new LoyaltyChargedLine(item.ItemIndex, item.ItemType, productId, item.AmountIn,
                allocations.GetValueOrDefault(item.ItemIndex),
                earnKind && !earnDiscountExcluded && !(productId is { } earnProduct && policy.EarnExcludedProductIds?.Contains(earnProduct) == true),
                redeemKind && !redeemDiscountExcluded && !(productId is { } redeemProduct && policy.RedeemExcludedProductIds?.Contains(redeemProduct) == true));
        }).ToList();
    }
}

internal sealed record LoyaltyChargedLine(int ItemIndex, string ItemType, Guid? ProductId,
    decimal OriginalCharged, decimal CouponDiscount, bool EarnEligible, bool RedeemEligible, decimal GiftFundedValue = 0m);
