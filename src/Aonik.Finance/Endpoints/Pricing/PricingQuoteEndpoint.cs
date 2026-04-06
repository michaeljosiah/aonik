using Aonik.Finance.Contracts.Api.Pricing;
using Aonik.Finance.Contracts.Services.Pricing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Get a bill payment pricing quote";
            s.Description = "Calculates a pricing quote for a bill payment, including exchange rate, fees, and total amount.";
            s.Response(200, "Pricing quote generated successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Pricing"));
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
