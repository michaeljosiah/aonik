using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Public storefront product detail by slug — Active products only (Spec 042 §14).</summary>
public class GetCatalogProductEndpoint : EndpointWithoutRequest<ProductDto>
{
    private readonly IProductService _products;

    public GetCatalogProductEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/catalog/products/{slug}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get a public storefront product by slug.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var slug = Route<string>("slug") ?? string.Empty;
        var result = await _products.GetProductBySlugAsync(slug, ct);
        if (result is null || result.Status != ProductStatuses.Active)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
