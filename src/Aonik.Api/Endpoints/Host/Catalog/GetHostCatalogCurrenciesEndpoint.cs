using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Host.Catalog;

public class GetHostCatalogCurrenciesEndpoint : EndpointWithoutRequest<CatalogCurrencyResponse>
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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var includeInactive = Query<bool>("includeInactive", isRequired: false);
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var request = new Application.Models.Catalog.CatalogCurrencyListRequest(includeInactive, countryCode);
        var result = await _catalogService.GetCurrenciesAsync(request, ct);

        var response = new CatalogCurrencyResponse(
            result.Currencies.Select(currency => new CatalogCurrencyItemResponse(
                currency.Code,
                currency.Name)).ToList(),
            result.DefaultCurrencyCode);

        await Send.OkAsync(response, ct);
    }
}
