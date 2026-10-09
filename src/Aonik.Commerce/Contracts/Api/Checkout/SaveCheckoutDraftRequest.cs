using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Contracts.Api.Checkout;

/// <summary>Full replacement of the checkout form; incomplete sections may be saved.</summary>
public record SaveCheckoutDraftRequest(
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
