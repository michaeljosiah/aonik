using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Defines a selection slot on a build-your-own-box product (Spec 042 §12).</summary>
public class AddBundleSlotEndpoint : Endpoint<AddBundleSlotRequest, BundleSlotDto>
{
    private readonly IProductService _products;

    public AddBundleSlotEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Post("/commerce/admin/products/{productId:guid}/bundle-slots");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Add a build-your-own-box selection slot to a bundle product.");
    }

    public override async Task HandleAsync(AddBundleSlotRequest req, CancellationToken ct)
    {
        var productId = Route<Guid>("productId");
        var command = new AddBundleSlotCommand(
            productId,
            req.Name,
            req.MinItems,
            req.MaxItems,
            req.FromCategoryId,
            req.AllowDuplicates,
            req.SortOrder,
            req.Options?.Select(o => new AddBundleSlotOptionLine(o.ProductVariantId, o.PriceDelta)).ToList());

        var result = await _products.AddBundleSlotAsync(command, ct);
        await Send.OkAsync(result, ct);
    }
}
