using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Services.Catalog;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Catalog;

/// <summary>Active facet groups, ordered, with options/bands — the menu filter UI renders this
/// verbatim (Spec 070 §10). Adding a fifth group here needs zero frontend change (A4).</summary>
public class ListPublicFacetGroupsEndpoint : EndpointWithoutRequest<IReadOnlyList<FacetGroupDto>>
{
    private readonly IFacetGroupService _facets;

    public ListPublicFacetGroupsEndpoint(IFacetGroupService facets) => _facets = facets;

    public override void Configure()
    {
        Get("/commerce/catalog/facets");
        AllowAnonymous();
        Summary(s => s.Summary = "List the storefront's filter facets.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        StorefrontCacheHeaders.Apply(HttpContext);
        await Send.OkAsync(await _facets.ListPublicAsync(ct), ct);
    }
}
