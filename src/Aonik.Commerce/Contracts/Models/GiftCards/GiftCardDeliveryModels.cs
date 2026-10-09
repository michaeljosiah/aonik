using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Contracts.Models.GiftCards;

/// <summary>Validated purchase facts frozen with checkout; never reconstructed from a later cart draft.</summary>
public sealed record GiftCardPurchaseSnapshot(
    int ItemIndex, Guid OrderItemId, decimal FaceValue, string Currency,
    string DeliveryMethod, string RecipientName, string? RecipientEmail = null,
    DateTime? SendAtUtc = null, DateOnly? PostingDate = null,
    DeliveryAddressDto? PostalAddress = null, string? RecipientPhone = null,
    string? Message = null, string? SenderName = null, bool IncludeGreetingCard = false);

public static class GiftCardDeliveryMethods
{
    public const string Email = "Email";
    public const string Post = "Post";
    public const string InFoodBox = "InFoodBox";
}

public sealed record SentGiftCardDto(Guid DeliveryId, Guid OrderId, string DeliveryMethod,
    decimal FaceValue, string Currency, string RecipientName, string? MaskedRecipientEmail,
    DateTime? SendAtUtc, DateOnly? PostingDate, string Status, DateTime? LastSentAtUtc,
    string MaskedCode, DateTime? ExpiresAtUtc, bool CanResend);

public sealed record PhysicalGiftCardDto(Guid DeliveryId, Guid OrderId, string DeliveryMethod,
    decimal FaceValue, string Currency, string RecipientName, DateOnly? PostingDate,
    string Status, DateTime? CompletedAtUtc, string Version);

/// <summary>Secret-bearing private staff document; never embed in ordinary order or list responses.</summary>
public sealed record GiftCardPrintDto(Guid DeliveryId, Guid OrderId, string DeliveryMethod,
    string RecipientName, DeliveryAddressDto? PostalAddress, string? RecipientPhone,
    string? SenderName, string? Message, bool IncludeGreetingCard,
    decimal FaceValue, string Currency, string Code, DateTime? ExpiresAtUtc, string TermsVersion, string Version);
