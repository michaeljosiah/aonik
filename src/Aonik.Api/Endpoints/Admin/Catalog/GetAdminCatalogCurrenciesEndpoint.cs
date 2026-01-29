using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Admin.Catalog;

public class GetAdminCatalogCurrenciesEndpoint : EndpointWithoutRequest<CatalogCurrencyResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogCurrenciesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/currencies");
        Policies("AdminPolicy");
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
