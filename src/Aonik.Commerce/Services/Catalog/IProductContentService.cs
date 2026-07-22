using System.Text.Json;

using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>Option-dependent product content (Spec 067): authored default block + per-combination
/// variants, exact-selection resolution with withheld-declaration fallback. Facts are authored or
/// absent — never derived, never substituted.</summary>
public interface IProductContentService
{
    // ── Authoring ──
    Task<ProductContentDto> UpsertContentAsync(Guid productId, UpsertProductContentCommand command, CancellationToken ct = default);

    /// <summary>Clears <c>RequiresReview</c> without editing ("reviewed, still correct") — like
    /// the upsert, re-captures the block's all-defaults binding.</summary>
    Task<ProductContentDto> ConfirmContentReviewAsync(Guid productId, CancellationToken ct = default);

    Task<ProductContentVariantDto> AddVariantAsync(Guid productId, UpsertContentVariantCommand command, CancellationToken ct = default);
    Task<ProductContentVariantDto> UpdateVariantAsync(Guid variantId, UpsertContentVariantCommand command, CancellationToken ct = default);

    /// <summary>Soft-retire (V-C5) — the row remains for history and reactivation.</summary>
    Task DeactivateVariantAsync(Guid variantId, CancellationToken ct = default);

    // ── Reads ──
    /// <summary>§5 resolution. Null when the product has no default block (a defined state:
    /// content is optional per product). Selection null/empty resolves the standard preparation.</summary>
    Task<ResolvedContentDto?> ResolveAsync(Guid productId, JsonElement? selection, CancellationToken ct = default);

    Task<ContentCoverageDto> GetCoverageAsync(Guid productId, CancellationToken ct = default);
}
