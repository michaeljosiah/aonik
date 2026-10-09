using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Contracts.Models.GiftCards;

public sealed record GiftCardStorefrontOptions(bool Enabled = false, string Version = "",
    Guid ProductVariantId = default, decimal[]? Values = null, decimal? CustomMinimum = null,
    decimal? CustomMaximum = null, string[]? DeliveryMethods = null, decimal Postage = 0m,
    decimal GreetingCardPrice = 0m, string? Timezone = null, TimeOnly? EmailSendTime = null,
    DayOfWeek[]? PostingDays = null, string? TaxTreatment = null);

public sealed record GiftCardPurchaseSelection(decimal FaceValue, string DeliveryMethod, string RecipientName,
    string AcceptedVersion, string? RecipientEmail = null, DateOnly? SendDate = null,
    DateOnly? PostingDate = null, DeliveryAddressDto? PostalAddress = null, string? RecipientPhone = null,
    string? Message = null, string? SenderName = null, bool IncludeGreetingCard = false);

public sealed record GiftCardPurchaseDto(GiftCardPurchaseSelection Selection, DateTime? SendAtUtc,
    decimal Postage, decimal GreetingCardPrice);

public sealed record GiftCardOptionsDto(bool Enabled, string Version, Guid ProductVariantId,
    IReadOnlyList<decimal> Values, decimal? CustomMinimum, decimal? CustomMaximum,
    IReadOnlyList<string> DeliveryMethods, decimal Postage, decimal GreetingCardPrice,
    string? Timezone, TimeOnly? EmailSendTime, IReadOnlyList<DayOfWeek> PostingDays,
    string? TermsVersion, bool NeverExpires, int? ValidForDays, string? TaxTreatment);

public sealed record GiftCardTenderQuoteDto(decimal RequestedAmount, decimal MaxRedeemableAmount,
    decimal GiftAmount, decimal CardAmount, string? MaskedCode, string? ReasonCode = null);

public sealed record GiftCardCartResponse(Guid CartId, string CartVersion,
    GiftCardPurchaseDto? Purchase, CartDiscountQuoteDto Quote);
