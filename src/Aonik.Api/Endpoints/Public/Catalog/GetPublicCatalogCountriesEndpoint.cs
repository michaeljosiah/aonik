using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Public.Catalog;

public class GetPublicCatalogCountriesEndpoint : EndpointWithoutRequest<CatalogCountryResponse>
{
    private readonly IPublicCatalogService _catalogService;

    public GetPublicCatalogCountriesEndpoint(IPublicCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/public/catalog/countries");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        var onlyServiceCountries = Query<bool?>("onlyServiceCountries", isRequired: false) ?? true;
        var capabilityType = Query<string?>("capabilityType", isRequired: false);
        if (string.IsNullOrWhiteSpace(capabilityType))
        {
            capabilityType = "BILLPAY";
        }

        var request = new Application.Models.Catalog.CatalogCountryListRequest(onlyServiceCountries, capabilityType);
        var result = await _catalogService.GetCountriesAsync(request, ct);

        var response = new CatalogCountryResponse(
            result.Countries.Select(country => new CatalogCountryItemResponse(
                country.CountryCode,
                country.Name)).ToList());

        await Send.OkAsync(response, ct);
    }
}
