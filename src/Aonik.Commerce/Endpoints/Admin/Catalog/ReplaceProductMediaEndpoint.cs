using Aonik.Commerce.Contracts.Api.Catalog;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Full-replace of a product's ordered media URLs (Spec 070 §10) — list, reorder,
/// remove. Upload itself is the storefront-readiness wiring item (§3), not this endpoint.</summary>
public class ReplaceProductMediaEndpoint : Endpoint<ReplaceProductMediaRequest, IReadOnlyList<ProductMediaDto>>
{
    private readonly IProductService _products;

    public ReplaceProductMediaEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Put("/commerce/admin/products/{productId:guid}/media");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Replace a product's ordered media list.");
    }

    public override async Task HandleAsync(ReplaceProductMediaRequest req, CancellationToken ct)
    {
        // Null items is rejected by the service — a misspelled property must not clear the gallery.
        var result = await _products.ReplaceProductMediaAsync(
            Route<Guid>("productId"),
            new ReplaceProductMediaCommand(
                req.Items?.Select(i => new ProductMediaLine(i.Url, i.Kind)).ToList()),
            ct);
        await Send.OkAsync(result, ct);
    }
}
