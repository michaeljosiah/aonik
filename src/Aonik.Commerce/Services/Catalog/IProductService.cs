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

    Task<ProductVariantDto> AddVariantAsync(AddVariantCommand command, CancellationToken cancellationToken = default);
    Task<ProductCategoryDto> CreateCategoryAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default);

    /// <summary>Defines a selection slot on a bundle product (build-your-own-box, §12).</summary>
    Task<BundleSlotDto> AddBundleSlotAsync(AddBundleSlotCommand command, CancellationToken cancellationToken = default);
}
