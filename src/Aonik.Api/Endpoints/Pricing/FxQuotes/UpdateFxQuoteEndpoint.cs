using Aonik.Api.Contracts.Pricing;
using Aonik.Application.Services.Pricing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Pricing.FxQuotes;

public class UpdateFxQuoteEndpoint : Endpoint<UpdateFxQuoteRequest, FxQuoteDetailResponse>
{
    private readonly IFxQuoteService _fxQuoteService;

    public UpdateFxQuoteEndpoint(IFxQuoteService fxQuoteService)
    {
        _fxQuoteService = fxQuoteService;
    }

    public override void Configure()
    {
        Put("/fx-quotes/{id}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpdateFxQuoteRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var request = new Application.Models.Pricing.UpdateFxQuoteRequest(
            req.Rate,
            req.ExpiresAt,
            req.Provider,
            req.MetadataJson);

        var result = await _fxQuoteService.UpdateAsync(id, request, ct);

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

        await Send.OkAsync(response, ct);
    }
}
