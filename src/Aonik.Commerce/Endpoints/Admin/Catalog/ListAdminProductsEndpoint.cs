using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

public class ListAdminProductsEndpoint : EndpointWithoutRequest<PagedResult<ProductSummaryDto>>
{
    private readonly IProductService _products;

    public ListAdminProductsEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/admin/products");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List catalog products with optional kind/category/status/search filters.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var query = new ListProductsQuery(
            Kind: Query<string?>("kind", isRequired: false),
            CategoryId: Query<Guid?>("categoryId", isRequired: false),
            Status: Query<string?>("status", isRequired: false),
            Search: Query<string?>("search", isRequired: false),
            Page: Query<int?>("page", isRequired: false) ?? 1,
            PageSize: Query<int?>("pageSize", isRequired: false) ?? 50);

        var result = await _products.ListProductsAsync(query, ct);
        await Send.OkAsync(result, ct);
    }
}
