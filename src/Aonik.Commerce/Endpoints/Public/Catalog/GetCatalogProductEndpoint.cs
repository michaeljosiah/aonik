using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Public storefront product detail by slug — Active products only (Spec 042 §14).</summary>
public class GetCatalogProductEndpoint : EndpointWithoutRequest<ProductDto>
{
    private readonly IProductService _products;
    private readonly IProductContentService _content;

    public GetCatalogProductEndpoint(IProductService products, IProductContentService content)
    {
        _products = products;
        _content = content;
    }

    public override void Configure()
    {
        Get("/commerce/catalog/products/{slug}");
        AllowAnonymous();
        Summary(s => s.Summary = "Get a public storefront product by slug.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);

        var slug = Route<string>("slug") ?? string.Empty;
        var result = await _products.GetProductBySlugAsync(slug, ct);
        if (result is null || result.Status != ProductStatuses.Active)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Spec 067 §8 — the product page renders its content panels from this first call: embed
        // the RESOLVED standard preparation (empty-selection resolution, flags included), never
        // the raw block. Null when no default block is authored.
        var content = await _content.ResolveAsync(result.Id, selection: null, ct);
        if (content is not null)
        {
            result = result with { Content = content, ContentVersion = content.ContentVersion };
        }

        await Send.OkAsync(result, ct);
    }
}
