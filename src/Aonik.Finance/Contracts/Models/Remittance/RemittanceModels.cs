using Aonik.Finance.Contracts.Models.Pricing;

namespace Aonik.Finance.Contracts.Models.Remittance;

/// <summary>
/// Service-model request for a remittance quote. Reuses the pricing engine but is persisted with
/// <c>PricingQuote.QuoteType = "Remittance"</c>. Spec 036 §6.3 / §10.1.
/// </summary>
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
/// Service-model confirm request. The endpoint also supplies the normalized <c>Idempotency-Key</c>
/// header separately to <see cref="Services.Remittance.IRemittanceOrderService.ConfirmAsync"/>.
/// Spec 036 §6.4 / §10.2.
/// </summary>
public record ConfirmRemittanceRequest(
    Guid PricingQuoteId,
    Guid CustomerPartyId,
    Guid DestinationExternalAccountId,
    string PurposeCode,
    string? Narration,
    string? ProviderCode,
    IReadOnlyDictionary<string, string>? Metadata);

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

/// <summary>
/// Immutable quote-lock snapshot stored on the order at confirm time. Holds only intent and the
/// non-sensitive destination summary; raw rail values stay behind <c>ExternalPayoutAccount.VaultRef</c>
/// or transient connector DTOs. Persisted into <c>OrderItem.DetailsJson</c> and <c>Order.ProvenanceJson</c>.
/// Spec 036 §5.2.
/// </summary>
public sealed record RemittanceOrderDetails(
    Guid PricingQuoteId,
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
    decimal RateMarkup,
    Guid PricingPolicyId,
    string PricingPolicyVersion,
    DateTime QuoteExpiresAt,
    string PurposeCode,
    string? Narration,
    Guid ConnectorId,
    string ProviderCode);
