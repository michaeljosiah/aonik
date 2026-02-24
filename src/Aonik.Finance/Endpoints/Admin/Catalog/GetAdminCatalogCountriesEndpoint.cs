using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Admin.Catalog;

internal class GetAdminCatalogCountriesEndpoint : EndpointWithoutRequest<CatalogCountryResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogCountriesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/countries");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var onlyServiceCountries = Query<bool>("onlyServiceCountries", isRequired: false);
        var capabilityType = Query<string?>("capabilityType", isRequired: false);
        var request = new CatalogCountryListRequest(onlyServiceCountries, capabilityType);
        var result = await _catalogService.GetCountriesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
