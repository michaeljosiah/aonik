using Aonik.Finance.Contracts.Api.Pricing;

namespace Aonik.Finance.Contracts.Api.Remittance;

/// <summary>HTTP request for <c>POST /payabo/remittance/quote</c>. Spec 036 §10.1.</summary>
public record RemittanceQuoteRequest(
    Guid CustomerPartyId,
    string OriginCountry,
    string DestinationCountry,
    string OriginCurrency,
    string DestinationCurrency,
    decimal? OriginAmount,
    decimal? DestinationAmount,
    string? CustomerTier,
    string? PurposeCode,
    string? ServiceCode);

public record RemittanceQuoteResponse(
    Guid PricingQuoteId,
    string QuoteType,
    string OriginCountry,
    string DestinationCountry,
    string OriginCurrency,
    string DestinationCurrency,
    decimal OriginAmount,
    decimal DestinationAmount,
    decimal FeesTotal,
    decimal TotalAmount,
    decimal ExchangeRate,
    decimal RateMarkup,
    Guid PricingPolicyId,
    string PricingPolicyVersion,
    Guid FxRateId,
    DateTimeOffset RateTimestamp,
    DateTimeOffset ExpiresAt,
    IReadOnlyCollection<FeeBreakdownItem> FeeBreakdown,
    IReadOnlyCollection<RemittanceDestinationMethod> SupportedDestinationMethods);

public record RemittanceDestinationMethod(
    string DestinationType,
    string CountryCode,
    string Currency,
    string? Network);

/// <summary>
/// HTTP body for <c>POST /payabo/remittance/confirm</c>. The <c>Idempotency-Key</c> is read from the
/// request header, not the body. Spec 036 §10.2.
/// </summary>
public record ConfirmRemittanceRequest(
    Guid PricingQuoteId,
    Guid CustomerPartyId,
    Guid DestinationExternalAccountId,
    string PurposeCode,
    string? Narration,
    string? ProviderCode,
    Dictionary<string, string>? Metadata);

public record RemittanceOrderResponse(
    Guid OrderId,
    string OrderNumber,
    string Status,
    Guid CustomerPartyId,
    Guid? BeneficiaryPartyId,
    Guid DestinationExternalAccountId,
    string DestinationType,
    string MaskedAccountIdentifier,
    string OriginCountry,
    string DestinationCountry,
    string OriginCurrency,
    string DestinationCurrency,
    decimal OriginAmount,
    decimal DestinationAmount,
    decimal FeesTotal,
    decimal TotalAmount,
    decimal ExchangeRate,
    Guid PricingQuoteId,
    Guid? PayoutId,
    string? ProviderCode,
    string? ClientReference,
    string? ProviderReference,
    string? TransmissionStatus,
    DateTime CreatedAt,
    DateTime? SubmittedAt,
    DateTime? SettledAt);
