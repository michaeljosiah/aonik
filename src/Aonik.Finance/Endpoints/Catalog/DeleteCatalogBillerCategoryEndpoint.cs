using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Aonik.Finance.Contracts.Services.Catalog;

namespace Aonik.Finance.Endpoints.Catalog;

internal class DeleteCatalogBillerCategoryEndpoint : EndpointWithoutRequest
{
    private readonly ICatalogService _catalogService;

    public DeleteCatalogBillerCategoryEndpoint(ICatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    public override void Configure()
    {
        Delete("/catalog/billers/categories/{categoryId}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Delete biller category";
            s.Description = "Soft-deletes a tenant-scoped biller category. Fails if the category still has billers.";
            s.Response(204, "Category deleted");
            s.Response(400, "Category still has billers");
            s.Response(401, "Not authenticated");
            s.Response(403, "Caller lacks Catalog.Write");
            s.Response(404, "Category not found");
        });
        Options(x => x.WithTags("Product Catalog"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var categoryId = Route<Guid>("categoryId");
        await _catalogService.DeleteCategoryAsync(categoryId, ct);
        await Send.NoContentAsync(ct);
    }
}
