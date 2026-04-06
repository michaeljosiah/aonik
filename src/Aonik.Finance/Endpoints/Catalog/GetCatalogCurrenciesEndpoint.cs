using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetCatalogCurrenciesEndpoint : EndpointWithoutRequest<CatalogCurrencyResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogCurrenciesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/currencies");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "List currencies";
            s.Description = "Returns available currencies for the current tenant, optionally filtered by country and active status.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var includeInactive = Query<bool>("includeInactive", isRequired: false);
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var request = new CatalogCurrencyListRequest(includeInactive, countryCode);
        var result = await _catalogService.GetCurrenciesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
