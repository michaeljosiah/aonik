using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class CreateProductEndpoint : Endpoint<CreateProductRequest, ProductDto>
{
    private readonly IProductService _products;

    public CreateProductEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Post("/commerce/admin/products");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Create a catalog product (Simple, Variant, or Bundle).");
    }

    public override async Task HandleAsync(CreateProductRequest req, CancellationToken ct)
    {
        var command = new CreateProductCommand(
            req.Slug,
            req.Name,
            req.Kind,
            req.Description ?? string.Empty,
            req.Status ?? "Active",
            req.CategoryId,
            req.TagsJson,
            req.AttributesJson,
            req.Variants?.Select(v => new CreateVariantLine(v.Sku, v.Name, v.OptionsJson, v.WeightGrams)).ToList(),
            req.BundlePricingMode,
            req.BundleFixedAmount,
            req.BundlePremium,
            req.BundleCurrency);

        var result = await _products.CreateProductAsync(command, ct);
        await Send.OkAsync(result, ct);
    }
}
