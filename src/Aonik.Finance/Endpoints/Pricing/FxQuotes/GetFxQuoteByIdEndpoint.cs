using Aonik.Finance.Contracts.Api.Pricing;
using Aonik.Finance.Contracts.Services.Pricing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Pricing.FxQuotes;

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
        Summary(s =>
        {
            s.Summary = "Get an FX quote by ID";
            s.Description = "Retrieves the full details of a foreign exchange rate quote by its unique identifier.";
            s.Response(200, "FX quote retrieved successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "FX quote not found");
        });
        Options(x => x.WithTags("Pricing"));
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
