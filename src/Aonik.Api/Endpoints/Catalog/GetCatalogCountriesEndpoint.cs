using FastEndpoints;
using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Catalog;

public class GetCatalogCountriesEndpoint : EndpointWithoutRequest<CatalogCountryResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogCountriesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/countries");
        Policies("UserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var request = new Application.Models.Catalog.CatalogCountryListRequest(Query<bool>("onlyServiceCountries"));
        var result = await _catalogService.GetCountriesAsync(request, ct);

        var response = new CatalogCountryResponse(
            result.Countries.Select(country => new CatalogCountryItemResponse(
                country.CountryCode,
                country.Name)).ToList());

        await Send.OkAsync(response, ct);
    }
}
