using System.Globalization;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Read-only coupon/tax quoting over the cart's existing priced goods.</summary>
internal sealed class CartDiscountQuotes(CommerceDbContext db, ITenantProvider tenantProvider,
    IDiscountService discounts, ITaxCalculator tax, ITenantSettingStore settingStore,
    ISettingProvider settings, ITenantCurrencyProvider tenantCurrency, CheckoutLoyaltyQuotes? loyaltyQuotes = null,
    GiftCardPurchasePricing? giftPricing = null, CheckoutGiftCards? giftCards = null)
{
    public async Task<CartDiscountQuoteDto> CalculateAsync(Cart cart, IReadOnlyList<OrderItemCommand> items,
        decimal delivery, string? code, CancellationToken ct = default)
    {
        var pricedItems = items.ToList();
        if (delivery > 0m) pricedItems.Add(new OrderItemCommand(CheckoutService.DeliveryFeeItemType,
            pricedItems.Count == 0 ? 0 : pricedItems.Max(x => x.ItemIndex) + 1, delivery, cart.Currency));
        if (giftPricing != null && !pricedItems.Any(x => x.ItemType == CartLineKinds.GiftCardValue))
            await giftPricing.AppendAsync(cart, pricedItems, ct);
        items = pricedItems;
        var subtotal = items.Where(x => x.ItemType != CheckoutService.DeliveryFeeItemType).Sum(x => x.AmountIn);
        var giftValue = items.Where(x => x.ItemType == CartLineKinds.GiftCardValue).Sum(x => x.AmountIn);
        DiscountCodeStatusDto? status = null;
        var computation = new DiscountComputation(null, null, 0m, []);
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim().ToUpperInvariant();
            try
            {
                var lines = await CheckoutDiscountLines.FromOrderItemsAsync(db, tenantProvider.GetCurrentTenantId(), items, ct);
                var result = await discounts.ComputeAsync(normalized, lines, cart.Currency, ct);
                computation = result;
                status = new DiscountCodeStatusDto(result.Code ?? normalized, result.Amount);
            }
            catch (DiscountException error)
            {
                status = new DiscountCodeStatusDto(normalized, 0m, error.Code, error.Message);
            }
        }
        var amount = computation.Amount;
        var taxTotal = await tax.CalculateAsync(subtotal - amount - giftValue, cart.Currency, ct);
        CheckoutLoyaltyQuote? loyalty = null;
        var requested = CartDraftData.Read(cart)?.RequestedPoints ?? 0;
        if (loyaltyQuotes is not null)
        {
            var chargedItems = items.ToList();
            loyalty = await loyaltyQuotes.CalculateAsync(cart, chargedItems, computation,
                subtotal - amount + taxTotal + delivery, ct);
        }
        else if (requested > 0)
            loyalty = new(null, new(requested, 0, 0, 0m, 0, ReasonCode: LoyaltyQuoteReasons.Disabled,
                Message: "Loyalty redemption is not currently available."));
        var points = loyalty?.PointsAppliedValue ?? 0m;
        if (points != 0m) taxTotal = await tax.CalculateAsync(subtotal - amount - points - giftValue, cart.Currency, ct);
        var total = subtotal - amount - points + taxTotal + delivery;
        var fundingItems = items.ToList();
        var giftFunding = giftCards == null ? null : await giftCards.QuoteAsync(cart, fundingItems, computation,
            loyalty?.Checkout, total, taxTotal, ct);
        if (loyalty?.Checkout is { } original && giftFunding?.Checkout?.Tender is { } tender)
        {
            var adjusted = LoyaltyCheckoutCalculator.ApplyGiftFunding(original, tender.Lines);
            loyalty = new(adjusted, loyalty.Quote is null ? null : loyalty.Quote with { EstimatedEarnedPoints = adjusted.EarnedPoints });
        }
        return new CartDiscountQuoteDto(cart.Id, Convert.ToBase64String(cart.RowVersion), cart.Currency,
            subtotal, amount, taxTotal, delivery, total, status,
            points, loyalty?.Quote, CheckoutGiftCards.Public(cart, giftFunding));
    }

    public async Task<CartDiscountQuoteDto> SnapshotAsync(Cart cart, string? code, CancellationToken ct = default)
    {
        if (!CartWriteGuard.IsEditable(cart))
        {
            if (cart.CheckoutPreparationJson is not null)
            {
                var frozen = CheckoutPreparation.Read(cart);
                return Frozen(cart, frozen.Subtotal, frozen.DiscountTotal, frozen.TaxTotal, frozen.Total,
                    frozen.DiscountCode, frozen.Loyalty?.PointsAppliedValue ?? 0m, frozen.Loyalty)
                    with { GiftCard = CheckoutGiftCards.Frozen(cart, frozen.GiftCard) };
            }
            if (cart.OrderId is { } orderId)
            {
                var summary = await db.OrderChargeSummaries.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.TenantId == cart.TenantId && x.OrderId == orderId, ct);
                if (summary is not null)
                    return Frozen(cart, summary.Subtotal, summary.DiscountTotal, summary.TaxTotal, summary.Total,
                        summary.DiscountCode, summary.PointsAppliedValue, CheckoutLoyaltyData.Read(summary.LoyaltyJson))
                        with { GiftCard = CheckoutGiftCards.Frozen(cart, CheckoutGiftCards.Read(summary.GiftCardJson)) };
            }
        }

        var items = new List<OrderItemCommand>();
        var delivery = 0m;
        if (cart.BoxBundleProductId is { } bundleId)
        {
            var plan = await db.BundleSizePlans.AsNoTracking().Include(x => x.Presets)
                .SingleOrDefaultAsync(x => x.TenantId == cart.TenantId && x.BundleProductId == bundleId, ct)
                ?? throw new StorefrontValidationException("This box has no size plan.");
            if (cart.BoxSize is not { } size || !BoxPricing.IsValidSize(plan, size)
                || !string.Equals(plan.Currency, cart.Currency, StringComparison.OrdinalIgnoreCase))
                throw new StorefrontValidationException("This box size is no longer available.");
            var dishes = cart.Items.Where(x => !x.IsDeleted && x.LineKind == CartLineKinds.BoxDish && x.BoxBundleSlotId is not null);
            var goods = BoxPricing.BoxPrice(plan, size)
                + dishes.Sum(x => ((x.PersonalisationAdjustment ?? 0m) + (x.UnitSurcharge ?? 0m)) * x.Quantity);
            items.Add(new OrderItemCommand(OrderTypeCodes.ProductPurchase, 0, goods, cart.Currency, ProductId: bundleId));
            foreach (var line in cart.Items.Where(x => !x.IsDeleted && x.LineKind == CartLineKinds.AddOn).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                items.Add(new OrderItemCommand(OrderTypeCodes.ProductPurchase, items.Count,
                    (line.UnitPriceSnapshot + (line.PersonalisationAdjustment ?? 0m) + (line.UnitSurcharge ?? 0m)) * line.Quantity,
                    cart.Currency, ProductId: line.ProductVariantId));
            var greetingCard = await GreetingCardPricing.ResolveAsync(settingStore, cart.TenantId, cart.Currency,
                CartDraftData.Read(cart)?.Gift, ct);
            if (greetingCard > 0m) items.Add(GreetingCardPricing.Item(items.Count, greetingCard, cart.Currency));
            var currency = await tenantCurrency.GetTenantDefaultCurrencyAsync(cart.TenantId, ct) ?? "GBP";
            if (string.Equals(currency, cart.Currency, StringComparison.OrdinalIgnoreCase))
            {
                var raw = await settingStore.GetTenantValueAsync(CommerceSettingNames.StorefrontDeliveryChargedAmount, cart.TenantId, ct)
                    ?? await settings.GetAsync(CommerceSettingNames.StorefrontDeliveryChargedAmount, ct);
                if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var configured) && configured >= 0)
                    delivery = configured;
            }
        }
        else
        {
            foreach (var item in cart.Items.Where(x => !x.IsDeleted && x.LineKind != CartLineKinds.GiftCardValue).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                items.Add(new OrderItemCommand(OrderTypeCodes.ProductPurchase, items.Count, item.UnitPriceSnapshot * item.Quantity,
                    cart.Currency, ProductId: item.IsBundle ? item.BundleProductId : item.ProductVariantId));
        }
        return await CalculateAsync(cart, items, delivery, code, ct);
    }

    private static CartDiscountQuoteDto Frozen(Cart cart, decimal subtotal, decimal discount, decimal tax, decimal total,
        string? code, decimal points, LoyaltyCheckout? loyalty)
        => new(cart.Id, Convert.ToBase64String(cart.RowVersion), cart.Currency, subtotal, discount, tax,
            total - (subtotal - discount - points + tax), total, code is null ? null : new DiscountCodeStatusDto(code, discount),
            points, CheckoutLoyaltyQuotes.Frozen(loyalty));
}
