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
    long RequestedPoints = 0, GiftCardDraftDto? GiftCardDraft = null);

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

/// <summary>Partial standalone/in-box configuration, not proof of a priced or payable purchase.</summary>
public sealed record GiftCardDraftDto(decimal Value = 100m, string Route = "email", int Quantity = 1,
    string? Message = null, bool IncludeGreetingCard = false, string? Email = null,
    string? FirstName = null, string? LastName = null, string? RecipientEmail = null,
    string? Line1 = null, string? Line2 = null, string? City = null, string? Region = null,
    string? Postcode = null, string? Phone = null, string? Date = null,
    bool CreateAccount = false, bool Removed = false, bool Dismissed = false);
