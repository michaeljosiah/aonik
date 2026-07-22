using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>The active category tree, children sorted (Spec 070 §10). A deactivated category
/// hides its whole subtree; the admin surface still lists it (A17).</summary>
public class GetCategoryTreeEndpoint : EndpointWithoutRequest<IReadOnlyList<CategoryTreeNodeDto>>
{
    private readonly IProductService _products;

    public GetCategoryTreeEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/catalog/categories");
        AllowAnonymous();
        Summary(s => s.Summary = "Get the storefront's active category tree.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);
        await Send.OkAsync(await _products.GetCategoryTreeAsync(ct), ct);
    }
}
