namespace Aonik.Commerce.Contracts.Models.Checkout;

public record DiscountCodeStatusDto(string Code, decimal Amount, string? ReasonCode = null, string? Message = null);

public record CartDiscountQuoteDto(Guid CartId, string CartVersion, string Currency, decimal Subtotal,
    decimal DiscountTotal, decimal TaxTotal, decimal DeliveryTotal, decimal Total, DiscountCodeStatusDto? Discount,
    decimal PointsAppliedValue = 0m, LoyaltyQuoteDto? Loyalty = null,
    Aonik.Commerce.Contracts.Models.GiftCards.GiftCardTenderQuoteDto? GiftCard = null);
