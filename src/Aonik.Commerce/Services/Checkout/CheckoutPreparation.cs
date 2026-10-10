using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Promotions;
using Aonik.SharedKernel.Abstractions.Billing;
using Aonik.SharedKernel.Abstractions.Loyalty;
using Aonik.SharedKernel.Abstractions.Ordering;

namespace Aonik.Commerce.Services.Checkout;

// Private resume data, separate from the customer's editable checkout draft. Never expose this
// record on an endpoint: it includes immutable provider inputs and fulfillment snapshots.
internal sealed record CheckoutPreparation(
    Guid AttemptId, Guid? GuestPartyId, string Currency, string Provider, string PaymentMethodType,
    string? ReturnUrl, string? CancelUrl, Guid? CustomerAccountId,
    decimal Subtotal, decimal DiscountTotal, Guid? DiscountId, string? DiscountCode, decimal TaxTotal, decimal Total,
    IReadOnlyList<OrderItemCommand> Items, IReadOnlyList<InvoiceLineSpec> InvoiceLines,
    IReadOnlyList<CheckoutStockLine> Stock, IReadOnlyList<CheckoutSelection> Selections, OrderDeliveryDto? Delivery,
    Guid? DeliveryReservationId = null, DateTime? ProviderStartDeadlineUtc = null, bool CreateAccount = false,
    Guid? DiscountReservationId = null, IReadOnlyList<DiscountAllocation>? DiscountAllocations = null,
    decimal GreetingCardCharged = 0m, LoyaltyCheckout? Loyalty = null,
    Aonik.SharedKernel.Abstractions.GiftCards.GiftCardCheckout? GiftCard = null,
    Aonik.Commerce.Contracts.Models.GiftCards.GiftCardPurchaseSnapshot? GiftCardDelivery = null,
    CheckoutContactDto? Purchaser = null,
    IReadOnlyList<Aonik.Commerce.Contracts.Models.GiftCards.GiftCardPurchaseSnapshot>? AdditionalGiftCardDeliveries = null)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
        { RespectRequiredConstructorParameters = true };

    public string Serialize()
    {
        var json = JsonSerializer.Serialize(this, Json);
        if (json.Length > 262144) throw new InvalidOperationException("Checkout snapshot is too large.");
        return json;
    }

    public static CheckoutPreparation Read(Entities.Cart.Cart cart) =>
        JsonSerializer.Deserialize<CheckoutPreparation>(cart.CheckoutPreparationJson
            ?? throw new InvalidOperationException("Checkout preparation is missing."), Json)
        ?? throw new InvalidOperationException("Checkout preparation is invalid.");
}

internal sealed record CheckoutStockLine(Guid ProductVariantId, decimal Quantity);
internal sealed record CheckoutSelection(int OrderItemIndex, Guid BundleSlotId, Guid ProductVariantId,
    decimal Quantity, string Sku, string? PersonalisationJson = null, string? PersonalisationSummary = null,
    decimal? PersonalisationAdjustment = null, decimal UnitSurcharge = 0m, string? PersonalisationEnvelopeJson = null,
    string? NameSnapshot = null, bool? IsSignatureSnapshot = null);
