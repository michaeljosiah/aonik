using Aonik.Api.Contracts.Pricing;
using Aonik.Application.Services.Pricing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Pricing.FxQuotes;

public class CreateFxQuoteEndpoint : Endpoint<CreateFxQuoteRequest, FxQuoteDetailResponse>
{
    private readonly IFxQuoteService _fxQuoteService;

    public CreateFxQuoteEndpoint(IFxQuoteService fxQuoteService)
    {
        _fxQuoteService = fxQuoteService;
    }

    public override void Configure()
    {
        Post("/fx-quotes");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateFxQuoteRequest req, CancellationToken ct)
    {
        var request = new Application.Models.Pricing.CreateFxQuoteRequest(
            req.BaseCurrency,
            req.TargetCurrency,
            req.Rate,
            req.ExpiresAt,
            req.Provider,
            req.MetadataJson);

        var result = await _fxQuoteService.CreateAsync(request, ct);

        var response = new FxQuoteDetailResponse(
            result.Id,
            result.TenantId,
            result.BaseCurrency,
            result.TargetCurrency,
            result.Rate,
            result.ExpiresAt,
            result.Provider,
            result.MetadataJson,
            result.CreatedAt,
            result.UpdatedAt);

        await Send.CreatedAtAsync<GetFxQuoteByIdEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
