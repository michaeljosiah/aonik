using System.Text.Json;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class CheckoutGiftCards(IGiftCardService gifts)
{
    public async Task<GiftCardFundingQuote?> QuoteAsync(Cart cart, IReadOnlyList<OrderItemCommand> items,
        DiscountComputation discount, LoyaltyCheckout? loyalty, decimal total, decimal tax,
        CancellationToken ct = default)
    {
        var tender = ReadTender(cart);
        if (tender == null) return null;
        if (total <= 0m || items.Count == 0)
            return new(tender.RequestedAmount, 0m, 0m, Math.Max(0m, total), null, "gift_card.no_payable_value");
        var coupons = discount.Allocations.ToDictionary(x => x.ItemIndex, x => x.Amount);
        var points = loyalty?.Lines.ToDictionary(x => x.ItemIndex, x => x.PointsAppliedValue);
        return await gifts.QuoteAsync(new(cart.Id, tender.PrivateCartGrant, cart.Currency, total, tax,
            tender.RequestedAmount, items.Select(x => new GiftCardFundingLine(x.ItemIndex, Guid.Empty, x.ItemType,
                x.AmountIn, coupons.GetValueOrDefault(x.ItemIndex), points?.GetValueOrDefault(x.ItemIndex) ?? 0m)).ToList()), ct);
    }

    public static GiftCardTenderQuoteDto? Public(Cart cart, GiftCardFundingQuote? quote) => quote == null ? null
        : new(quote.RequestedAmount, quote.MaxRedeemableAmount, quote.GiftAmount, quote.CardAmount,
            ReadTender(cart)?.MaskedCode, quote.ReasonCode);

    public static GiftCardTenderQuoteDto? Frozen(Cart cart, GiftCardCheckout? checkout) => checkout?.Tender is not { } tender ? null
        : new(tender.Amount, tender.Amount, tender.Amount, tender.ExpectedCardAmount, ReadTender(cart)?.MaskedCode);

    public static StoredGiftCardTender? ReadTender(Cart cart) => cart.GiftCardTenderJson == null ? null
        : JsonSerializer.Deserialize<StoredGiftCardTender>(cart.GiftCardTenderJson, GiftCardPurchasePricing.Json);

    public static string? Serialize(GiftCardCheckout? value) => value == null ? null
        : JsonSerializer.Serialize(value, GiftCardPurchasePricing.Json);
    public static GiftCardCheckout? Read(string? json) => json == null ? null
        : JsonSerializer.Deserialize<GiftCardCheckout>(json, GiftCardPurchasePricing.Json);
}

internal sealed record StoredGiftCardTender(string PrivateCartGrant, decimal RequestedAmount, string MaskedCode);
