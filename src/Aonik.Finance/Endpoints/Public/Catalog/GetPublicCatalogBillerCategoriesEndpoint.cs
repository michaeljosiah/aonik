using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Aonik.Finance.Endpoints.Catalog;

namespace Aonik.Finance.Endpoints.Public.Catalog;

internal class GetPublicCatalogBillerCategoriesEndpoint : Endpoint<CatalogCategoryListRequest, CatalogBillerCategoryResponse>
{
    private readonly IPublicCatalogService _catalogService;

    public GetPublicCatalogBillerCategoriesEndpoint(IPublicCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/public/catalog/billers/categories");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "List biller categories (public)";
            s.Description = "Returns biller categories for the tenant specified in the X-Tenant-Id header. No authentication required.";
            s.Response(200, "Success");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CatalogCategoryListRequest req, CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        var countryCode = CatalogValidation.NormalizeCountryCode(req.CountryCode);
        if (req.CountryCode != null && countryCode == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "countryCode must be ISO-3166-1 alpha-2." }, ct);
            return;
        }

        var request = new CatalogCategoryListRequest(countryCode);
        var result = await _catalogService.GetCategoriesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
