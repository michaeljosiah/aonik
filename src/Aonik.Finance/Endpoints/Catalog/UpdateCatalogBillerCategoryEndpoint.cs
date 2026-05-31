using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Models.Catalog;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

/// <summary>
/// PATCH-style update wrapped behind PUT (idempotent body merge). All fields
/// are optional; null/missing values mean "leave unchanged".
/// </summary>
internal class UpdateCatalogBillerCategoryEndpoint : Endpoint<UpdateCatalogBillerCategoryRequest, CatalogBillerCategoryItem>
{
    private readonly ICatalogService _catalogService;

    public UpdateCatalogBillerCategoryEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Put("/catalog/billers/categories/{categoryId}");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Update biller category";
            s.Description = "Updates a tenant-scoped biller category. All body fields are optional.";
            s.Response(200, "Category updated");
            s.Response(400, "Invalid request");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Category not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(UpdateCatalogBillerCategoryRequest req, CancellationToken ct)
    {
        var categoryId = Route<Guid>("categoryId");
        var result = await _catalogService.UpdateCategoryAsync(categoryId, req, ct);
        await Send.OkAsync(result, ct);
    }
}
