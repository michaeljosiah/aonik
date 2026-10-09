using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Payments;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class CheckoutRefundSourceReader(CommerceDbContext db, ITenantProvider tenantProvider,
    IOrderService orders) : ICheckoutRefundSourceReader
{
    public async Task<CheckoutRefundSource?> ReadAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var summary = await db.OrderChargeSummaries.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId
            && x.OrderId == orderId && !x.IsDeleted, cancellationToken);
        if (summary is null) return null;
        if (summary.PaymentStatus != "Captured") throw new InvalidStateException("The checkout has not been paid.");
        var order = await orders.GetAsync(orderId, cancellationToken)
            ?? throw new InvalidStateException("The original paid order is unavailable.");
        var loyalty = CheckoutLoyaltyData.Read(summary.LoyaltyJson);
        var gift = CheckoutGiftCards.Read(summary.GiftCardJson);
        var discounts = summary.DiscountAllocationsJson is null ? []
            : DiscountAllocationSnapshot.Read(summary.DiscountAllocationsJson);
        if (discounts.Sum(x => x.Amount) != summary.DiscountTotal
            || discounts.Select(x => x.OrderItemId).Distinct().Count() != discounts.Count
            || (loyalty?.PointsAppliedValue ?? 0m) != summary.PointsAppliedValue
            || order.TenantId != tenantId || order.CurrencyIn != summary.Currency)
            throw new InvalidStateException("The original checkout allocations are unavailable.");
        var components = new List<CheckoutRefundComponent>();
        foreach (var item in order.Items.OrderBy(x => x.ItemIndex))
        {
            var coupon = discounts.SingleOrDefault(x => x.OrderItemId == item.Id)?.Amount ?? 0m;
            var points = loyalty?.Lines.SingleOrDefault(x => x.OrderItemId == item.Id);
            var tender = gift?.Tender?.Lines.SingleOrDefault(x => x.OrderItemId == item.Id);
            var amount = item.AmountIn - coupon - (points?.PointsAppliedValue ?? 0m);
            if (amount < 0 || coupon < 0 || (points is not null && (points.OriginalCharged != item.AmountIn
                    || points.CouponDiscount != coupon || points.EarnedPoints < 0 || points.RedeemedPoints < 0
                    || points.GiftFundedValue != (tender?.GiftFundedValue ?? 0m)))
                || (tender is not null && (tender.OriginalCharged != item.AmountIn || tender.CouponDiscount != coupon
                    || tender.PointsAppliedValue != (points?.PointsAppliedValue ?? 0m))))
                throw new InvalidStateException("A paid line's original allocation does not reconcile.");
            if (amount == 0 && (points?.RedeemedPoints ?? 0) == 0 && (points?.EarnedPoints ?? 0) == 0) continue;
            components.Add(new(item.Id.ToString("N"), item.Id, item.ItemType,
                item.NameSnapshot ?? item.Sku ?? item.ItemType, amount, tender?.GiftFundedValue ?? 0m,
                points?.EarnedPoints ?? 0, points?.RedeemedPoints ?? 0));
        }
        if (summary.TaxTotal > 0)
            components.Add(new("tax", null, "Tax", "Tax", summary.TaxTotal, gift?.Tender?.GiftFundedTaxAmount ?? 0m, 0, 0));
        if (order.Items.Where(x => x.ItemType != "DeliveryFee").Sum(x => x.AmountIn) != summary.Subtotal || components.Sum(x => x.Amount) != summary.Total
            || components.Sum(x => x.GiftFundedValue) != (gift?.Tender?.Amount ?? 0m)
            || components.Any(x => x.GiftFundedValue < 0 || x.GiftFundedValue > x.Amount)
            || discounts.Any(x => !order.Items.Any(i => i.Id == x.OrderItemId))
            || (loyalty is not null && (components.Sum(x => x.EarnedPoints) != loyalty.EarnedPoints
                || components.Sum(x => x.RedeemedPoints) != loyalty.RedeemedPoints)))
            throw new InvalidStateException("The original charge breakdown does not reconcile.");
        return new(orderId, summary.PaymentIntentId, summary.InvoiceId, summary.Currency, summary.Total,
            gift?.Tender?.Amount ?? 0m, summary.Total - (gift?.Tender?.Amount ?? 0m), components);
    }
}
