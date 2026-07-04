using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class AddVariantEndpoint : Endpoint<AddVariantRequest, ProductVariantDto>
{
    private readonly IProductService _products;

    public AddVariantEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Post("/commerce/admin/products/{productId:guid}/variants");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Add a variant to a product.");
    }

    public override async Task HandleAsync(AddVariantRequest req, CancellationToken ct)
    {
        var productId = Route<Guid>("productId");
        var result = await _products.AddVariantAsync(
            new AddVariantCommand(productId, req.Sku, req.Name, req.OptionsJson, req.WeightGrams), ct);
        await Send.OkAsync(result, ct);
    }
}
