using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

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
