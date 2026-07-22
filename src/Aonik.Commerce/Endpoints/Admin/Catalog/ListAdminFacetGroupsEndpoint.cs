using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Catalog;

/// <summary>All facet groups including inactive — reactivation needs to see them (Spec 070 §10).</summary>
public class ListAdminFacetGroupsEndpoint : EndpointWithoutRequest<IReadOnlyList<FacetGroupDto>>
{
    private readonly IFacetGroupService _facets;

    public ListAdminFacetGroupsEndpoint(IFacetGroupService facets) => _facets = facets;

    public override void Configure()
    {
        Get("/commerce/admin/facet-groups");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List all facet groups, inactive included.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _facets.ListAdminAsync(ct), ct);
}
