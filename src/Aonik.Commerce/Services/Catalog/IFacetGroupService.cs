using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Storefront filter facet definitions (Spec 070 §5/§6/§11). Adding, renaming, reordering
/// or retiring groups is data, not code — the storefront renders whatever these return.</summary>
public interface IFacetGroupService
{
    /// <summary>Active groups, ordered, options parsed — the menu filter UI renders this verbatim.</summary>
    Task<IReadOnlyList<FacetGroupDto>> ListPublicAsync(CancellationToken cancellationToken = default);

    /// <summary>All groups including inactive — reactivation needs to see them.</summary>
    Task<IReadOnlyList<FacetGroupDto>> ListAdminAsync(CancellationToken cancellationToken = default);

    Task<FacetGroupDto> CreateAsync(CreateFacetGroupCommand command, CancellationToken cancellationToken = default);
    Task<FacetGroupDto> UpdateAsync(Guid facetGroupId, UpdateFacetGroupCommand command, CancellationToken cancellationToken = default);
}
