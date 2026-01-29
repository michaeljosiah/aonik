using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Catalog;

public class GetCatalogCurrenciesEndpoint : EndpointWithoutRequest<CatalogCurrencyResponse>
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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var includeInactive = Query<bool>("includeInactive", isRequired: false);
        var request = new Application.Models.Catalog.CatalogCurrencyListRequest(includeInactive);
        var result = await _catalogService.GetCurrenciesAsync(request, ct);

        var response = new CatalogCurrencyResponse(
            result.Currencies.Select(currency => new CatalogCurrencyItemResponse(
                currency.Code,
                currency.Name)).ToList());

        await Send.OkAsync(response, ct);
    }
}
