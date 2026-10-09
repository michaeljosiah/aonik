namespace Aonik.Commerce.Contracts.Models.Checkout;

/// <summary>Editable checkout choices. Partial forms are allowed; checkout validates completeness.</summary>
public record CartCheckoutDraftDto(
    CheckoutContactDto? Purchaser = null,
    DeliveryAddressDto? Address = null,
    DeliveryRecipientDto? Recipient = null,
    DateOnly? DeliveryDate = null,
    string? Notes = null,
    CartGiftDraftDto? Gift = null,
    bool CreateAccount = false,
    string? DiscountCode = null,
    string? AcceptedTermsVersion = null,
    long RequestedPoints = 0);

public record CartGiftDraftDto(
    bool GiftIntent = false,
    bool HidePrices = true,
    bool IncludeGreetingCard = false,
    string? GreetingCardMessage = null);

public record CartCheckoutDraftResponse(
    Guid CartId,
    string CartVersion,
    string Status,
    Guid? OrderId,
    CartCheckoutDraftDto? Draft);
