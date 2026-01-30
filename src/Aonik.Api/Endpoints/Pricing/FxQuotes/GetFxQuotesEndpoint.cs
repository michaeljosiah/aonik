using Aonik.Api.Contracts.Pricing;
using Aonik.Application.Services.Pricing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Pricing.FxQuotes;

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
