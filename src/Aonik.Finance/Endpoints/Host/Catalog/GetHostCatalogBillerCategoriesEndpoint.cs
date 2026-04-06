using FastEndpoints;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "List biller categories (host admin)";
            s.Description = "Returns biller categories across all tenants for platform host administrators.";
            s.Response(200, "Success");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var countryCode = Query<string?>("countryCode", isRequired: false);
        var request = new CatalogCategoryListRequest(countryCode);
        var result = await _catalogService.GetCategoriesAsync(request, ct);
        await Send.OkAsync(result, ct);
    }
}
