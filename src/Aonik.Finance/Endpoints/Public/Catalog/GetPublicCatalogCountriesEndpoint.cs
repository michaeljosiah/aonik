using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Public.Catalog;

internal class GetPublicCatalogCountriesEndpoint : EndpointWithoutRequest<CatalogCountryResponse>
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
            capabilityType = "BILLPAY";

        var request = new CatalogCountryListRequest(onlyServiceCountries, capabilityType);
        var result = await _catalogService.GetCountriesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
