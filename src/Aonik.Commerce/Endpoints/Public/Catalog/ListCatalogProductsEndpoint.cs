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
        StorefrontCacheHeaders.Apply(HttpContext);

        var query = new ListProductsQuery(
            Kind: Query<string?>("kind", isRequired: false),
            CategoryId: Query<Guid?>("categoryId", isRequired: false),
            Status: ProductStatuses.Active,
            Search: Query<string?>("search", isRequired: false),
            Page: Query<int?>("page", isRequired: false) ?? 1,
            PageSize: Query<int?>("pageSize", isRequired: false) ?? 50,
            Facets: ParseFacetParameters(),
            Collection: Query<string?>("collection", isRequired: false),
            Sort: Query<string?>("sort", isRequired: false));

        var result = await _products.ListProductsAsync(query, ct);
        await Send.OkAsync(result, ct);
    }

    /// <summary>Spec 070 §6 — repeatable <c>facet.&lt;key&gt;=value1,value2</c> parameters.
    /// Repeated occurrences of the same key merge; values are option tokens, never labels.
    /// Validation (unknown keys/values → 400) is the service's job — this only parses shape.</summary>
    private Dictionary<string, IReadOnlyList<string>>? ParseFacetParameters()
    {
        Dictionary<string, IReadOnlyList<string>>? facets = null;

        foreach (var parameter in HttpContext.Request.Query)
        {
            if (!parameter.Key.StartsWith("facet.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = parameter.Key["facet.".Length..];
            var values = parameter.Value
                .SelectMany(v => (v ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToList();

            facets ??= new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            facets[key] = facets.TryGetValue(key, out var existing)
                ? existing.Concat(values).ToList()
                : values;
        }

        return facets;
    }
}
