using System.Text.Json;

using Aonik.Application.Abstractions.Multitenancy;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Models.Pricing;
using Aonik.Application.Services.Compliance;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Services.Pricing;

public class PricingService : IPricingService
{
    private readonly ITenantProvider _tenantProvider;
    private readonly IPricingPolicyService _pricingPolicyService;
    private readonly IFxRateService _fxRateService;
    private readonly ICurrencyMetadataProvider _currencyMetadataProvider;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IAonikDbContext _dbContext;

    public PricingService(
        ITenantProvider tenantProvider,
        IPricingPolicyService pricingPolicyService,
        IFxRateService fxRateService,
        ICurrencyMetadataProvider currencyMetadataProvider,
        IAuditLogWriter auditLogWriter,
        IAonikDbContext dbContext)
    {
        _tenantProvider = tenantProvider;
        _pricingPolicyService = pricingPolicyService;
        _fxRateService = fxRateService;
        _currencyMetadataProvider = currencyMetadataProvider;
        _auditLogWriter = auditLogWriter;
        _dbContext = dbContext;
    }

    public async Task<PricingQuoteResponse> GetBillPaymentQuoteAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = NormalizeRequest(request);
        ValidateRequest(normalizedRequest);

        var customerTier = await ResolveCustomerTierAsync(normalizedRequest, cancellationToken);
        var policyResolution = await _pricingPolicyService.ResolvePolicyAsync(
            normalizedRequest,
            customerTier,
            cancellationToken);

        var fxRate = await _fxRateService.GetRateAsync(
            normalizedRequest.OriginCurrency,
            normalizedRequest.DestinationCurrency,
            cancellationToken);

        if (fxRate.Rate <= 0m)
        {
            throw new InvalidOperationException("FX rate is invalid for the requested currency pair.");
        }

        var roundingMode = ResolveRoundingMode(policyResolution.Conditions.RoundingMode);
        var originPrecision = _currencyMetadataProvider.GetCurrency(normalizedRequest.OriginCurrency).DecimalPlaces;
        var destinationPrecision = _currencyMetadataProvider.GetCurrency(normalizedRequest.DestinationCurrency).DecimalPlaces;

        var markupRate = (policyResolution.Conditions.MarkupBps ?? 0) / 10000m;
        var exchangeRate = fxRate.Rate * (1 - markupRate);

        if (markupRate >= 1m)
        {
            throw new InvalidOperationException("FX markup is invalid for the requested currency pair.");
        }

        if (exchangeRate <= 0m)
        {
            throw new InvalidOperationException("Effective FX rate is invalid for the requested currency pair.");
        }

        var (originAmount, destinationAmount) = ResolveAmounts(
            normalizedRequest,
            exchangeRate,
            originPrecision,
            destinationPrecision,
            roundingMode);

        var fixedFee = RoundCurrency(policyResolution.Policy.FixedFee, originPrecision, roundingMode);
        var percentageFee = RoundCurrency(originAmount * policyResolution.Policy.PercentageFee, originPrecision, roundingMode);
        var uncappedFeesTotal = RoundCurrency(fixedFee + percentageFee, originPrecision, roundingMode);
        var feesTotal = ApplyFeeCaps(uncappedFeesTotal, policyResolution.Conditions, originPrecision, roundingMode);

        var totalAmount = RoundCurrency(originAmount + feesTotal, originPrecision, roundingMode);
        var pricingQuoteId = Guid.NewGuid();

        await ValidateLimitsAsync(
            normalizedRequest,
            originAmount,
            cancellationToken);

        var feeBreakdown = BuildFeeBreakdown(
            policyResolution.Conditions,
            fixedFee,
            percentageFee,
            uncappedFeesTotal,
            feesTotal,
            originAmount,
            destinationAmount,
            fxRate.Rate,
            exchangeRate,
            normalizedRequest.OriginCurrency,
            originPrecision,
            roundingMode);

        var response = new PricingQuoteResponse(
            pricingQuoteId,
            exchangeRate,
            markupRate,
            feesTotal,
            totalAmount,
            originAmount,
            destinationAmount,
            policyResolution.Policy.Id,
            policyResolution.Version,
            fxRate.FxRateId,
            fxRate.RateTimestamp,
            feeBreakdown);

        await WriteAuditAsync(normalizedRequest, response, cancellationToken);

        return response;
    }

    private PricingQuoteRequest NormalizeRequest(PricingQuoteRequest request)
    {
        return request with
        {
            OriginCurrency = NormalizeCurrency(request.OriginCurrency),
            DestinationCurrency = NormalizeCurrency(request.DestinationCurrency),
            OriginCountry = NormalizeCountry(request.OriginCountry),
            DestinationCountry = NormalizeCountry(request.DestinationCountry),
            ServiceCode = NormalizeServiceCode(request.ServiceCode),
            CustomerTier = string.IsNullOrWhiteSpace(request.CustomerTier)
                ? null
                : request.CustomerTier.Trim(),
            QuoteContext = string.IsNullOrWhiteSpace(request.QuoteContext)
                ? null
                : request.QuoteContext.Trim()
        };
    }

    private void ValidateRequest(PricingQuoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginCurrency))
            throw new ArgumentException("Origin currency is required.", nameof(request.OriginCurrency));

        if (string.IsNullOrWhiteSpace(request.DestinationCurrency))
            throw new ArgumentException("Destination currency is required.", nameof(request.DestinationCurrency));

        if (request.OriginCurrency.Length != 3)
            throw new ArgumentException("Origin currency must be a 3-letter ISO 4217 code.", nameof(request.OriginCurrency));

        if (request.DestinationCurrency.Length != 3)
            throw new ArgumentException("Destination currency must be a 3-letter ISO 4217 code.", nameof(request.DestinationCurrency));

        if (string.IsNullOrWhiteSpace(request.OriginCountry))
            throw new ArgumentException("Origin country is required.", nameof(request.OriginCountry));

        if (string.IsNullOrWhiteSpace(request.DestinationCountry))
            throw new ArgumentException("Destination country is required.", nameof(request.DestinationCountry));

        if (request.OriginCountry.Length != 2)
            throw new ArgumentException("Origin country must be a 2-letter ISO 3166-1 alpha-2 code.", nameof(request.OriginCountry));

        if (request.DestinationCountry.Length != 2)
            throw new ArgumentException("Destination country must be a 2-letter ISO 3166-1 alpha-2 code.", nameof(request.DestinationCountry));

        if (string.IsNullOrWhiteSpace(request.ServiceCode))
            throw new ArgumentException("Service code is required.", nameof(request.ServiceCode));

        if (request.OriginAmount.HasValue == request.DestinationAmount.HasValue)
            throw new ArgumentException("Exactly one of originAmount or destinationAmount must be provided.");

        if (request.OriginAmount.HasValue && request.OriginAmount.Value <= 0)
            throw new ArgumentException("Origin amount must be greater than zero.", nameof(request.OriginAmount));

        if (request.DestinationAmount.HasValue && request.DestinationAmount.Value <= 0)
            throw new ArgumentException("Destination amount must be greater than zero.", nameof(request.DestinationAmount));

        _currencyMetadataProvider.GetCurrency(request.OriginCurrency);
        _currencyMetadataProvider.GetCurrency(request.DestinationCurrency);
    }

    private async Task<string> ResolveCustomerTierAsync(
        PricingQuoteRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomerTier))
        {
            return request.CustomerTier.Trim();
        }

        if (!request.CustomerId.HasValue)
        {
            return "Retail";
        }

        var party = await _dbContext.Parties
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == request.CustomerId.Value, cancellationToken);

        if (party == null || string.IsNullOrWhiteSpace(party.CustomerTierCode))
        {
            return "Retail";
        }

        return party.CustomerTierCode.Trim();
    }

    private static (decimal OriginAmount, decimal DestinationAmount) ResolveAmounts(
        PricingQuoteRequest request,
        decimal exchangeRate,
        int originPrecision,
        int destinationPrecision,
        MidpointRounding roundingMode)
    {
        if (request.DestinationAmount.HasValue)
        {
            var destinationAmount = RoundCurrency(request.DestinationAmount.Value, destinationPrecision, roundingMode);
            var originAmount = RoundCurrency(destinationAmount / exchangeRate, originPrecision, roundingMode);
            return (originAmount, destinationAmount);
        }

        var originValue = RoundCurrency(request.OriginAmount!.Value, originPrecision, roundingMode);
        var destinationValue = RoundCurrency(originValue * exchangeRate, destinationPrecision, roundingMode);
        return (originValue, destinationValue);
    }

    private static decimal ApplyFeeCaps(
        decimal feesTotal,
        FeePolicyConditions conditions,
        int precision,
        MidpointRounding roundingMode)
    {
        var minFee = conditions.MinFee ?? 0m;
        var maxFee = conditions.MaxFee ?? 0m;

        if (minFee > 0m && feesTotal < minFee)
        {
            return RoundCurrency(minFee, precision, roundingMode);
        }

        if (maxFee > 0m && feesTotal > maxFee)
        {
            return RoundCurrency(maxFee, precision, roundingMode);
        }

        return feesTotal;
    }

    private async Task ValidateLimitsAsync(
        PricingQuoteRequest request,
        decimal originAmount,
        CancellationToken cancellationToken)
    {
        var limitsPolicy = await _pricingPolicyService.ResolveLimitsPolicyAsync(
            request.CustomerId,
            request.OriginCurrency,
            cancellationToken);

        if (limitsPolicy == null)
        {
            return;
        }

        if (originAmount > limitsPolicy.MaxAmount)
        {
            throw new InvalidOperationException("Requested amount exceeds corridor limits.");
        }
    }

    private async Task WriteAuditAsync(
        PricingQuoteRequest request,
        PricingQuoteResponse response,
        CancellationToken cancellationToken)
    {
        var auditPayload = new
        {
            request.OriginCurrency,
            request.DestinationCurrency,
            request.OriginCountry,
            request.DestinationCountry,
            request.ServiceCode,
            RequestOriginAmount = request.OriginAmount,
            RequestDestinationAmount = request.DestinationAmount,
            request.CustomerId,
            request.CustomerTier,
            request.QuoteContext,
            response.PricingPolicyId,
            response.PricingPolicyVersion,
            response.FxRateId,
            response.RateTimestamp,
            response.ExchangeRate,
            response.RateMarkup,
            response.FeesTotal,
            response.TotalAmount,
            ResponseOriginAmount = response.OriginAmount,
            ResponseDestinationAmount = response.DestinationAmount
        };

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var detailsJson = JsonSerializer.Serialize(auditPayload);

        await _auditLogWriter.LogAsync(
            AuditEventNames.PricingQuoteCreated,
            "PricingQuote",
            response.PricingQuoteId,
            tenantId,
            actorId: null,
            correlationId: null,
            detailsJson: detailsJson,
            cancellationToken: cancellationToken);
    }

    private static IReadOnlyCollection<FeeBreakdownItem> BuildFeeBreakdown(
        FeePolicyConditions conditions,
        decimal fixedFee,
        decimal percentageFee,
        decimal uncappedFeesTotal,
        decimal cappedFeesTotal,
        decimal originAmount,
        decimal destinationAmount,
        decimal baseRate,
        decimal exchangeRate,
        string originCurrency,
        int originPrecision,
        MidpointRounding roundingMode)
    {
        if (conditions.FeeBreakdown == null || conditions.FeeBreakdown.Count == 0)
        {
            return Array.Empty<FeeBreakdownItem>();
        }

        var items = new List<FeeBreakdownItem>();

        foreach (var definition in conditions.FeeBreakdown)
        {
            var amount = definition.CalculationType switch
            {
                "Fixed" => fixedFee,
                "Percentage" => percentageFee,
                "FxMarkup" => CalculateFxMarkup(originAmount, destinationAmount, baseRate, exchangeRate, originPrecision, roundingMode),
                _ => 0m
            };

            items.Add(new FeeBreakdownItem(
                definition.Code,
                definition.Description,
                amount,
                originCurrency,
                definition.CalculationType));
        }

        if (uncappedFeesTotal != cappedFeesTotal)
        {
            var adjustment = RoundCurrency(cappedFeesTotal - uncappedFeesTotal, originPrecision, roundingMode);
            items.Add(new FeeBreakdownItem(
                "FEE_CAP_ADJUSTMENT",
                "Fee cap adjustment",
                adjustment,
                originCurrency,
                "CapAdjustment"));
        }

        return items;
    }

    private static decimal CalculateFxMarkup(
        decimal originAmount,
        decimal destinationAmount,
        decimal baseRate,
        decimal exchangeRate,
        int originPrecision,
        MidpointRounding roundingMode)
    {
        if (exchangeRate == 0m || baseRate == 0m)
        {
            return 0m;
        }

        var originWithoutMarkup = RoundCurrency(destinationAmount / baseRate, originPrecision, roundingMode);
        var markupAmount = originAmount - originWithoutMarkup;
        return RoundCurrency(Math.Max(markupAmount, 0m), originPrecision, roundingMode);
    }

    private static decimal RoundCurrency(decimal amount, int precision, MidpointRounding roundingMode)
        => Math.Round(amount, precision, roundingMode);

    private static string NormalizeCurrency(string currency)
        => string.IsNullOrWhiteSpace(currency) ? string.Empty : currency.Trim().ToUpperInvariant();

    private static string NormalizeCountry(string country)
        => string.IsNullOrWhiteSpace(country) ? string.Empty : country.Trim().ToUpperInvariant();

    private static string NormalizeServiceCode(string serviceCode)
        => string.IsNullOrWhiteSpace(serviceCode) ? string.Empty : serviceCode.Trim().ToUpperInvariant();

    private static MidpointRounding ResolveRoundingMode(string? roundingMode)
    {
        if (string.IsNullOrWhiteSpace(roundingMode))
        {
            return MidpointRounding.AwayFromZero;
        }

        return roundingMode.Trim() switch
        {
            "ToEven" => MidpointRounding.ToEven,
            "AwayFromZero" => MidpointRounding.AwayFromZero,
            _ => MidpointRounding.AwayFromZero
        };
    }
}
