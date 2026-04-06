using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetCatalogCountriesEndpoint : EndpointWithoutRequest<CatalogCountryResponse>
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
        Summary(s =>
        {
            s.Summary = "List countries";
            s.Description = "Returns supported countries for the current tenant, optionally filtered by service availability and capability type.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
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
