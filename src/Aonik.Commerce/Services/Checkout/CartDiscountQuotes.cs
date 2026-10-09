using System.Globalization;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Abstractions.Settings;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>Read-only coupon/tax quoting over the cart's existing priced goods.</summary>
internal sealed class CartDiscountQuotes(CommerceDbContext db, ITenantProvider tenantProvider,
    IDiscountService discounts, ITaxCalculator tax, ITenantSettingStore settingStore,
    ISettingProvider settings, ITenantCurrencyProvider tenantCurrency)
{
    public async Task<CartDiscountQuoteDto> CalculateAsync(Cart cart, IReadOnlyList<OrderItemCommand> items,
        decimal delivery, string? code, CancellationToken ct = default)
    {
        var subtotal = items.Sum(x => x.AmountIn);
        DiscountCodeStatusDto? status = null;
        var amount = 0m;
        if (!string.IsNullOrWhiteSpace(code))
        {
            var normalized = code.Trim().ToUpperInvariant();
            try
            {
                var lines = await CheckoutDiscountLines.FromOrderItemsAsync(db, tenantProvider.GetCurrentTenantId(), items, ct);
                var result = await discounts.ComputeAsync(normalized, lines, cart.Currency, ct);
                amount = result.Amount;
                status = new DiscountCodeStatusDto(result.Code ?? normalized, amount);
            }
            catch (DiscountException error)
            {
                status = new DiscountCodeStatusDto(normalized, 0m, error.Code, error.Message);
            }
        }
        var taxTotal = await tax.CalculateAsync(subtotal - amount, cart.Currency, ct);
        return new CartDiscountQuoteDto(cart.Id, Convert.ToBase64String(cart.RowVersion), cart.Currency,
            subtotal, amount, taxTotal, delivery, subtotal - amount + taxTotal + delivery, status);
    }

    public async Task<CartDiscountQuoteDto> SnapshotAsync(Cart cart, string? code, CancellationToken ct = default)
    {
        if (!CartWriteGuard.IsEditable(cart))
        {
            if (cart.CheckoutPreparationJson is not null)
            {
                var frozen = CheckoutPreparation.Read(cart);
                return Frozen(cart, frozen.Subtotal, frozen.DiscountTotal, frozen.TaxTotal, frozen.Total, frozen.DiscountCode);
            }
            if (cart.OrderId is { } orderId)
            {
                var summary = await db.OrderChargeSummaries.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.TenantId == cart.TenantId && x.OrderId == orderId, ct);
                if (summary is not null)
                    return Frozen(cart, summary.Subtotal, summary.DiscountTotal, summary.TaxTotal, summary.Total, summary.DiscountCode);
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
            foreach (var item in cart.Items.Where(x => !x.IsDeleted).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
                items.Add(new OrderItemCommand(OrderTypeCodes.ProductPurchase, items.Count, item.UnitPriceSnapshot * item.Quantity,
                    cart.Currency, ProductId: item.IsBundle ? item.BundleProductId : item.ProductVariantId));
        }
        return await CalculateAsync(cart, items, delivery, code, ct);
    }

    private static CartDiscountQuoteDto Frozen(Cart cart, decimal subtotal, decimal discount, decimal tax, decimal total, string? code)
        => new(cart.Id, Convert.ToBase64String(cart.RowVersion), cart.Currency, subtotal, discount, tax,
            total - (subtotal - discount + tax), total, code is null ? null : new DiscountCodeStatusDto(code, discount));
}
