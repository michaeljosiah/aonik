using Aonik.Api.Contracts.Pricing;
using Aonik.Application.Services.Pricing;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Pricing.FxQuotes;

public class GetFxQuoteByIdEndpoint : EndpointWithoutRequest<FxQuoteDetailResponse>
{
    private readonly IFxQuoteService _fxQuoteService;

    public GetFxQuoteByIdEndpoint(IFxQuoteService fxQuoteService)
    {
        _fxQuoteService = fxQuoteService;
    }

    public override void Configure()
    {
        Get("/fx-quotes/{id}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var result = await _fxQuoteService.GetByIdAsync(id, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

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
