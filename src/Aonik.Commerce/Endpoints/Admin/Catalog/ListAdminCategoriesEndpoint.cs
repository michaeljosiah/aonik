using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>Every category including retired ones, with lifecycle state (Spec 070 A17). Without
/// this, deactivating a category made it undiscoverable — the only other read is the public
/// active-only tree, so the back office could never find the id to reactivate.</summary>
public class ListAdminCategoriesEndpoint : EndpointWithoutRequest<IReadOnlyList<ProductCategoryDto>>
{
    private readonly IProductService _products;

    public ListAdminCategoriesEndpoint(IProductService products) => _products = products;

    public override void Configure()
    {
        Get("/commerce/admin/categories");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List all categories, retired included.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _products.ListCategoriesAsync(ct), ct);
}
