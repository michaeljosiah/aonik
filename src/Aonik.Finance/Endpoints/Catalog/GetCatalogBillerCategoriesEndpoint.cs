using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class GetCatalogBillerCategoriesEndpoint : Endpoint<CatalogCategoryListRequest, CatalogBillerCategoryResponse>
{
    private readonly ICatalogService _catalogService;

    public GetCatalogBillerCategoriesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/catalog/billers/categories");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CatalogCategoryListRequest req, CancellationToken ct)
    {
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
