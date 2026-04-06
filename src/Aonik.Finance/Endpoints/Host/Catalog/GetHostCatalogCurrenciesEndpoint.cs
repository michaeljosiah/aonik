using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Host.Catalog;

internal class GetHostCatalogCurrenciesEndpoint : EndpointWithoutRequest<CatalogCurrencyResponse>
{
    private readonly ICatalogService _catalogService;

    public GetHostCatalogCurrenciesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/host/catalog/currencies");
        Policies("PlatformAdmin");
        Summary(s =>
        {
            s.Summary = "List currencies (host admin)";
            s.Description = "Returns available currencies across all tenants for platform host administrators.";
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
