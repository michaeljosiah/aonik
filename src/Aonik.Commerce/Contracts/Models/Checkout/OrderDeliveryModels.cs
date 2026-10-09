namespace Aonik.Commerce.Contracts.Models.Checkout;

public record CheckoutContactDto(string Email, string FirstName, string LastName, string Phone);

public record DeliveryAddressDto(
    string Line1, string? Line2, string City, string? Region, string Postcode, string CountryCode);

public record DeliveryRecipientDto(string Name, string Phone);

/// <summary>Gift food-box choices frozen with delivery; absence means an ordinary order.</summary>
public record OrderGiftDto(bool HidePrices, bool IncludeGreetingCard, string? GreetingCardMessage);

public record AcceptedSaleTermsDto(string Version, string Url, DateTime AcceptedAtUtc);

/// <summary>Submitted delivery facts. A missing recipient means delivery to the submitted purchaser.</summary>
public record CheckoutDeliveryDetails(
    CheckoutContactDto Purchaser,
    DeliveryAddressDto Address,
    DateOnly DeliveryDate,
    DeliveryRecipientDto? Recipient = null,
    string? Notes = null,
    string? WindowId = null);

/// <summary>Immutable checkout snapshot; timezone comes from the validated tenant calendar.</summary>
public record OrderDeliveryDto(
    CheckoutContactDto Purchaser,
    DeliveryAddressDto Address,
    DateOnly DeliveryDate,
    string Timezone,
    DeliveryRecipientDto Recipient,
    string? Notes = null,
    OrderGiftDto? Gift = null,
    AcceptedSaleTermsDto? SaleTerms = null);
