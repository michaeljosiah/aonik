using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Curated merchandising collections (Spec 070 §5/§10). Returns DTOs, never entities.</summary>
public interface ICollectionService
{
    // ── Public storefront reads (Active collections, Active members) ──
    Task<IReadOnlyList<PublicCollectionDto>> ListPublicAsync(string? kind = null, CancellationToken cancellationToken = default);
    Task<PublicCollectionDto?> GetPublicBySlugAsync(string slug, CancellationToken cancellationToken = default);

    // ── Admin (inactive collections and draft members included) ──
    Task<IReadOnlyList<AdminCollectionSummaryDto>> ListAdminAsync(CancellationToken cancellationToken = default);
    Task<AdminCollectionDto> GetAdminAsync(Guid collectionId, CancellationToken cancellationToken = default);
    Task<AdminCollectionDto> CreateAsync(CreateCollectionCommand command, CancellationToken cancellationToken = default);
    Task<AdminCollectionDto> UpdateAsync(Guid collectionId, UpdateCollectionCommand command, CancellationToken cancellationToken = default);

    /// <summary>Full-replace of the ranked membership — idempotent reorder. Ranks must be unique
    /// (A12); products must exist in the tenant, any status (a draft may be staged, A9).</summary>
    Task<AdminCollectionDto> ReplaceItemsAsync(Guid collectionId, ReplaceCollectionItemsCommand command, CancellationToken cancellationToken = default);
}
