using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>The missing product update (Spec 070 §10) — the catalog was create-only until now.
/// PATCH semantics: only supplied members apply; JSON fields validated on write (§11).</summary>
public class UpdateProductEndpoint : Endpoint<UpdateProductRequest, AdminProductDetailDto>
{
    private readonly IProductService _products;

    public UpdateProductEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Patch("/commerce/admin/products/{productId:guid}");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Partially update a product. Omitted members are unchanged.");
    }

    public override async Task HandleAsync(UpdateProductRequest req, CancellationToken ct)
    {
        var result = await _products.UpdateProductAsync(
            Route<Guid>("productId"),
            new UpdateProductCommand(
                req.Name, req.Description, req.Status,
                req.CategoryId, req.ClearCategory,
                req.TagsJson, req.AttributesJson, req.SearchKeywordsJson),
            ct);
        await Send.OkAsync(result, ct);
    }
}
