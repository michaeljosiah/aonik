using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>All collections including inactive ones — the back office must be able to load
/// what the public read hides (reactivation, staging; Spec 070 A9).</summary>
public class ListAdminCollectionsEndpoint : EndpointWithoutRequest<IReadOnlyList<AdminCollectionSummaryDto>>
{
    private readonly ICollectionService _collections;

    public ListAdminCollectionsEndpoint(ICollectionService collections) => _collections = collections;

    public override void Configure()
    {
        Get("/commerce/admin/collections");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List all collections, inactive included.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _collections.ListAdminAsync(ct), ct);
}
