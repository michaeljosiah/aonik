namespace Aonik.Application.Models.Pricing;

public record PricingQuoteRequest(
    string OriginCurrency,
    string DestinationCurrency,
    string OriginCountry,
    string DestinationCountry,
    string ServiceCode,
    decimal? DestinationAmount,
    decimal? OriginAmount,
    Guid? CustomerId,
    string? CustomerTier,
    string? QuoteContext);

public record PricingQuoteResponse(
    Guid PricingQuoteId,
    decimal ExchangeRate,
    decimal RateMarkup,
    decimal FeesTotal,
    decimal TotalAmount,
    decimal OriginAmount,
    decimal DestinationAmount,
    Guid PricingPolicyId,
    string PricingPolicyVersion,
    Guid FxRateId,
    DateTimeOffset RateTimestamp,
    IReadOnlyCollection<FeeBreakdownItem> FeeBreakdown);

public record FeeBreakdownItem(
    string Code,
    string Description,
    decimal Amount,
    string Currency,
    string CalculationType);
