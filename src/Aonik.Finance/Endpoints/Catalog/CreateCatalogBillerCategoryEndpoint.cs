using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class CreateCatalogBillerCategoryEndpoint : Endpoint<CreateCatalogBillerCategoryRequest, CatalogBillerCategoryItem>
{
    private readonly ICatalogService _catalogService;

    public CreateCatalogBillerCategoryEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Post("/catalog/billers/categories");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Create biller category";
            s.Description = "Creates a tenant-scoped biller category. Requires Catalog.Write.";
            s.Response(201, "Category created");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CreateCatalogBillerCategoryRequest req, CancellationToken ct)
    {
        var result = await _catalogService.CreateCategoryAsync(req, ct);
        await Send.CreatedAtAsync<GetCatalogBillerCategoriesEndpoint>(
            routeValues: null,
            responseBody: result,
            generateAbsoluteUrl: false,
            cancellation: ct);
    }
}
