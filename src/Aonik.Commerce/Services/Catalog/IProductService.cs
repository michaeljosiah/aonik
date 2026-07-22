using Aonik.Commerce.Contracts.Models.Catalog;

namespace Aonik.Commerce.Services.Catalog;

/// <summary>
/// Catalog management for the Commerce module (Spec 042 §8/§12): products, variants, categories,
/// media, and composite/build-your-own-box bundle definitions. Returns DTOs, never entities.
/// </summary>
public interface IProductService
{
    Task<ProductDto> CreateProductAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductAsync(Guid productId, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetProductBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<PagedResult<ProductSummaryDto>> ListProductsAsync(ListProductsQuery query, CancellationToken cancellationToken = default);

    /// <summary>Spec 070 §7 — the admin detail: ProductDto plus the hidden search keywords, which
    /// appear in no public response. The editor must see them or a full update would erase them.</summary>
    Task<AdminProductDetailDto?> GetAdminProductAsync(Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Spec 070 §10 — the missing product update. PATCH semantics: only supplied members
    /// apply; JSON fields validated on write (§11).</summary>
    Task<AdminProductDetailDto> UpdateProductAsync(Guid productId, UpdateProductCommand command, CancellationToken cancellationToken = default);

    /// <summary>Spec 070 §10 — full-replace of a product's ordered media (list/reorder/remove;
    /// upload is separate wiring).</summary>
    Task<IReadOnlyList<ProductMediaDto>> ReplaceProductMediaAsync(Guid productId, ReplaceProductMediaCommand command, CancellationToken cancellationToken = default);

    Task<ProductVariantDto> AddVariantAsync(AddVariantCommand command, CancellationToken cancellationToken = default);
    Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default);

    /// <summary>Spec 070 §10 — the public category tree: active categories only, children sorted.
    /// A category under a deactivated ancestor is unreachable and therefore absent (A17).</summary>
    Task<IReadOnlyList<CategoryTreeNodeDto>> GetCategoryTreeAsync(CancellationToken cancellationToken = default);

    /// <summary>Spec 070 §10/§11 — update name/parent/sort/IsActive. Categories retire, never
    /// delete. Re-parenting is cycle-checked; omitted members are unchanged.</summary>
    Task<ProductCategoryDto> UpdateCategoryAsync(Guid categoryId, UpdateCategoryCommand command, CancellationToken cancellationToken = default);

    /// <summary>Spec 070 A17 — every category including retired ones, flat. The back office needs
    /// this to rediscover a deactivated category's id; the public tree deliberately cannot.</summary>
    Task<IReadOnlyList<ProductCategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>Defines a selection slot on a bundle product (build-your-own-box, §12).</summary>
    Task<BundleSlotDto> AddBundleSlotAsync(AddBundleSlotCommand command, CancellationToken cancellationToken = default);
}
