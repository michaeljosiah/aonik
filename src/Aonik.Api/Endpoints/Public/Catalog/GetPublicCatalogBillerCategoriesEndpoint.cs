using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Catalog;
using Aonik.Api.Endpoints.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Public.Catalog;

public class GetPublicCatalogBillerCategoriesEndpoint : Endpoint<CatalogCategoryListRequest, CatalogBillerCategoryResponse>
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

        var appRequest = new Application.Models.Catalog.CatalogCategoryListRequest(countryCode);
        var result = await _catalogService.GetCategoriesAsync(appRequest, ct);

        var response = new CatalogBillerCategoryResponse(
            result.Categories.Select(category => new CatalogBillerCategoryItemResponse(
                category.CategoryId,
                category.Name,
                category.Description,
                category.IconUrl,
                category.CountryCode)).ToList());

        await Send.OkAsync(response, ct);
    }
}
