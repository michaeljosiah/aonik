using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Entities.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Public storefront catalog browse — Active products only (Spec 042 §14).</summary>
public class ListCatalogProductsEndpoint : EndpointWithoutRequest<PagedResult<ProductSummaryDto>>
{
    private readonly IProductService _products;

    public ListCatalogProductsEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/catalog/products");
        AllowAnonymous();
        Summary(s => s.Summary = "Browse the public storefront catalog (Active products).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new ListProductsQuery(
            Kind: Query<string?>("kind", isRequired: false),
            CategoryId: Query<Guid?>("categoryId", isRequired: false),
            Status: ProductStatuses.Active,
            Search: Query<string?>("search", isRequired: false),
            Page: Query<int?>("page", isRequired: false) ?? 1,
            PageSize: Query<int?>("pageSize", isRequired: false) ?? 50);

        var result = await _products.ListProductsAsync(query, ct);
        await Send.OkAsync(result, ct);
    }
}
