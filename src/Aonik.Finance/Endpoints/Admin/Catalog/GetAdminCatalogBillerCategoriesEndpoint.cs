using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Admin.Catalog;

internal class GetAdminCatalogBillerCategoriesEndpoint : EndpointWithoutRequest<CatalogBillerCategoryResponse>
{
    private readonly ICatalogService _catalogService;

    public GetAdminCatalogBillerCategoriesEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Get("/admin/catalog/billers/categories");
        Policies("AdminPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var request = new CatalogCategoryListRequest(countryCode);
        var result = await _catalogService.GetCategoriesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
