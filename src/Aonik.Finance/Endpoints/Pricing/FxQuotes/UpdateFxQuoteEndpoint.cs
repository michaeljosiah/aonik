using Aonik.Finance.Contracts.Api.Pricing;
using Aonik.Finance.Contracts.Services.Pricing;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Pricing.FxQuotes;

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
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Update an FX quote";
            s.Description = "Updates the rate, expiry, provider, or metadata of an existing FX quote.";
            s.Response(200, "FX quote updated successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "FX quote not found");
        });
        Options(x => x.WithTags("Pricing"));
    }

    public override async Task HandleAsync(UpdateFxQuoteRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var request = new Finance.Contracts.Models.Pricing.UpdateFxQuoteRequest(
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
