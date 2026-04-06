using Aonik.Finance.Contracts.Api.Pricing;
using Aonik.Finance.Contracts.Services.Pricing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Pricing.FxQuotes;

public class GetFxQuotesEndpoint : EndpointWithoutRequest<IReadOnlyCollection<FxQuoteListResponse>>
{
    private readonly IFxQuoteService _fxQuoteService;

    public GetFxQuotesEndpoint(IFxQuoteService fxQuoteService)
    {
        _fxQuoteService = fxQuoteService;
    }

    public override void Configure()
    {
        Get("/fx-quotes");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List FX quotes";
            s.Description = "Returns FX quotes, optionally filtered by base currency, target currency, and expiry status.";
            s.Response(200, "FX quotes retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Pricing"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var baseCurrency = Query<string?>("baseCurrency", isRequired: false);
        var targetCurrency = Query<string?>("targetCurrency", isRequired: false);
        var includeExpired = Query<bool>("includeExpired", isRequired: false);

        var result = await _fxQuoteService.GetAllAsync(
            baseCurrency,
            targetCurrency,
            includeExpired,
            ct);

        var response = result.Select(q => new FxQuoteListResponse(
            q.Id,
            q.BaseCurrency,
            q.TargetCurrency,
            q.Rate,
            q.ExpiresAt,
            q.Provider,
            q.CreatedAt,
            q.UpdatedAt)).ToList();

        await Send.OkAsync(response, ct);
    }
}
