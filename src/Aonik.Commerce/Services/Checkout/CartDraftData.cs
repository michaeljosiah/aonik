using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Services.Catalog;

namespace Aonik.Commerce.Services.Checkout;

internal static class CartDraftData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CartCheckoutDraftDto? Read(Cart cart)
        => cart.CheckoutDraftJson is null ? null : JsonSerializer.Deserialize<CartCheckoutDraftDto>(cart.CheckoutDraftJson, JsonOptions);

    public static string? Serialize(CartCheckoutDraftDto draft)
    {
        var normalized = Normalize(draft);
        if (normalized == new CartCheckoutDraftDto()) return null;
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        if (json.Length > 24000)
            throw new StorefrontValidationException("Checkout draft is too long.");
        return json;
    }

    public static CartCheckoutDraftDto Normalize(CartCheckoutDraftDto draft)
    {
        var purchaser = draft.Purchaser is { } p ? new CheckoutContactDto(
            Text(p.Email, 254, "Purchaser.Email") ?? "",
            Text(p.FirstName, 100, "Purchaser.FirstName") ?? "",
            Text(p.LastName, 100, "Purchaser.LastName") ?? "",
            Text(p.Phone, 32, "Purchaser.Phone") ?? "") : null;
        if (purchaser == new CheckoutContactDto("", "", "", "")) purchaser = null;

        var address = draft.Address is { } a ? new DeliveryAddressDto(
            Text(a.Line1, 200, "Address.Line1") ?? "", Text(a.Line2, 200, "Address.Line2"),
            Text(a.City, 100, "Address.City") ?? "", Text(a.Region, 100, "Address.Region"),
            Text(a.Postcode, 32, "Address.Postcode")?.ToUpperInvariant() ?? "",
            Text(a.CountryCode, 2, "Address.CountryCode")?.ToUpperInvariant() ?? "") : null;
        if (address == new DeliveryAddressDto("", null, "", null, "", "")) address = null;

        var recipient = draft.Recipient is { } r ? new DeliveryRecipientDto(
            Text(r.Name, 201, "Recipient.Name") ?? "", Text(r.Phone, 32, "Recipient.Phone") ?? "") : null;
        if (recipient == new DeliveryRecipientDto("", "")) recipient = null;
        var gift = draft.Gift is { GiftIntent: true } g ? g with
        {
            GreetingCardMessage = g.IncludeGreetingCard
                ? Text(g.GreetingCardMessage, 1000, "Gift.GreetingCardMessage", multiline: true) : null
        } : null;

        return draft with
        {
            Purchaser = purchaser,
            Address = address,
            Recipient = recipient,
            Notes = Text(draft.Notes, 1000, "Notes", multiline: true),
            Gift = gift,
            DiscountCode = Text(draft.DiscountCode, 64, "DiscountCode"),
            AcceptedTermsVersion = Text(draft.AcceptedTermsVersion, 128, "AcceptedTermsVersion")
        };
    }

    public static CheckoutDeliveryDetails? Delivery(CartCheckoutDraftDto? draft)
    {
        if (draft is null || (draft.Purchaser is null && draft.Address is null && draft.Recipient is null
            && draft.DeliveryDate is null && draft.Notes is null)) return null;
        return new CheckoutDeliveryDetails(
            draft.Purchaser ?? new CheckoutContactDto("", "", "", ""),
            draft.Address ?? new DeliveryAddressDto("", null, "", null, "", ""),
            draft.DeliveryDate ?? default, draft.Recipient, draft.Notes);
    }

    private static string? Text(string? value, int maxLength, string field, bool multiline = false)
    {
        if (value is null) return null;
        var trimmed = value.Trim();
        if (trimmed.Length > maxLength || value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n')))
            throw new StorefrontValidationException($"{field}: use at most {maxLength} characters without unsupported control characters.");
        return trimmed.Length == 0 ? null : trimmed;
    }
}
