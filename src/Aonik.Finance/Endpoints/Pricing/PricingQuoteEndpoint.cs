using Aonik.Finance.Contracts.Api.Pricing;
using Aonik.Finance.Contracts.Services.Pricing;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Pricing;

public class PricingQuoteEndpoint : Endpoint<PricingQuoteRequest, PricingQuoteResponse>
{
    private readonly IPricingService _pricingService;

    public PricingQuoteEndpoint(IPricingService pricingService)
    {
        _pricingService = pricingService;
    }

    public override void Configure()
    {
        Post("/pricing/quote");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(PricingQuoteRequest req, CancellationToken ct)
    {
        var request = new Finance.Contracts.Models.Pricing.PricingQuoteRequest(
            req.OriginCurrency,
            req.DestinationCurrency,
            req.OriginCountry,
            req.DestinationCountry,
            req.ServiceCode,
            req.DestinationAmount,
            req.OriginAmount,
            req.CustomerId,
            req.CustomerTier,
            req.QuoteContext);

        var result = await _pricingService.GetBillPaymentQuoteAsync(request, ct);

        var response = new PricingQuoteResponse(
            result.PricingQuoteId,
            result.ExchangeRate,
            result.RateMarkup,
            result.FeesTotal,
            result.TotalAmount,
            result.OriginAmount,
            result.DestinationAmount,
            result.PricingPolicyId,
            result.PricingPolicyVersion,
            result.FxRateId,
            result.RateTimestamp,
            result.FeeBreakdown.Select(item => new FeeBreakdownItem(
                item.Code,
                item.Description,
                item.Amount,
                item.Currency,
                item.CalculationType)).ToList());

        await Send.OkAsync(response, ct);
    }
}
