using FastEndpoints;

using Aonik.Api.Contracts.Catalog;
using Aonik.Application.Services.Catalog;

namespace Aonik.Api.Endpoints.Host.Catalog;

public class GetHostCatalogBillerCategoriesEndpoint : EndpointWithoutRequest<CatalogBillerCategoryResponse>
{
    private readonly ICatalogService _catalogService;

    public GetHostCatalogBillerCategoriesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/host/catalog/billers/categories");
        Policies("PlatformAdmin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var request = new Application.Models.Catalog.CatalogCategoryListRequest(countryCode);
        var result = await _catalogService.GetCategoriesAsync(request, ct);

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
