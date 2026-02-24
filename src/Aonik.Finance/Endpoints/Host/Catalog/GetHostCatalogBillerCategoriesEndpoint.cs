using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Host.Catalog;

internal class GetHostCatalogBillerCategoriesEndpoint : EndpointWithoutRequest<CatalogBillerCategoryResponse>
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
        var request = new CatalogCategoryListRequest(countryCode);
        var result = await _catalogService.GetCategoriesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
