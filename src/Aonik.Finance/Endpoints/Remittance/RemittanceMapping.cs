using ApiContracts = Aonik.Finance.Contracts.Api.Remittance;
using ApiPricing = Aonik.Finance.Contracts.Api.Pricing;
using Models = Aonik.Finance.Contracts.Models.Remittance;

namespace Aonik.Finance.Endpoints.Remittance;

/// <summary>
/// Maps between the HTTP-facing remittance contracts (<c>Contracts.Api.Remittance</c>) and the service
/// models (<c>Contracts.Models.Remittance</c>), following the Api/Models split used by Orders and Pricing.
/// </summary>
internal static class RemittanceMapping
{
    public static Models.RemittanceQuoteRequest ToModel(ApiContracts.RemittanceQuoteRequest req)
        => new(
            req.CustomerPartyId,
            req.OriginCountry,
            req.DestinationCountry,
            req.OriginCurrency,
            req.DestinationCurrency,
            req.OriginAmount,
            req.DestinationAmount,
            req.CustomerTier,
            req.PurposeCode,
            req.ServiceCode);

    public static Models.ConfirmRemittanceRequest ToModel(ApiContracts.ConfirmRemittanceRequest req)
        => new(
            req.PricingQuoteId,
            req.CustomerPartyId,
            req.DestinationExternalAccountId,
            req.PurposeCode,
            req.Narration,
            req.ProviderCode,
            req.Metadata);

    public static ApiContracts.RemittanceQuoteResponse ToApi(Models.RemittanceQuoteResponse result)
        => new(
            result.PricingQuoteId,
            result.QuoteType,
            result.OriginCountry,
            result.DestinationCountry,
            result.OriginCurrency,
            result.DestinationCurrency,
            result.OriginAmount,
            result.DestinationAmount,
            result.FeesTotal,
            result.TotalAmount,
            result.ExchangeRate,
            result.RateMarkup,
            result.PricingPolicyId,
            result.PricingPolicyVersion,
            result.FxRateId,
            result.RateTimestamp,
            result.ExpiresAt,
            result.FeeBreakdown
                .Select(f => new ApiPricing.FeeBreakdownItem(f.Code, f.Description, f.Amount, f.Currency, f.CalculationType))
                .ToList(),
            result.SupportedDestinationMethods
                .Select(m => new ApiContracts.RemittanceDestinationMethod(m.DestinationType, m.CountryCode, m.Currency, m.Network))
                .ToList());

    public static ApiContracts.RemittanceOrderResponse ToApi(Models.RemittanceOrderResponse result)
        => new(
            result.OrderId,
            result.OrderNumber,
            result.Status,
            result.CustomerPartyId,
            result.BeneficiaryPartyId,
            result.DestinationExternalAccountId,
            result.DestinationType,
            result.MaskedAccountIdentifier,
            result.OriginCountry,
            result.DestinationCountry,
            result.OriginCurrency,
            result.DestinationCurrency,
            result.OriginAmount,
            result.DestinationAmount,
            result.FeesTotal,
            result.TotalAmount,
            result.ExchangeRate,
            result.PricingQuoteId,
            result.PayoutId,
            result.ProviderCode,
            result.ClientReference,
            result.ProviderReference,
            result.TransmissionStatus,
            result.CreatedAt,
            result.SubmittedAt,
            result.SettledAt);
}
