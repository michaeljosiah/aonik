namespace Aonik.Commerce.Contracts.Models.Catalog;

/// <summary>A page of results. Shared by the catalog list endpoints/services (Spec 042).</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

public record ProductPriceDto(
    Guid Id,
    Guid ProductVariantId,
    string Currency,
    decimal Amount,
    DateTime? EffectiveFrom,
    DateTime? EffectiveTo,
    bool IsActive);

public record ProductVariantDto(
    Guid Id,
    Guid ProductId,
    string Sku,
    string Name,
    string OptionsJson,
    decimal? WeightGrams,
    bool IsActive,
    IReadOnlyList<ProductPriceDto> Prices);

public record ProductMediaDto(Guid Id, string Url, string Kind, int SortOrder);

public record BundleSlotOptionDto(Guid Id, Guid ProductVariantId, decimal? PriceDelta);

public record BundleSlotDto(
    Guid Id,
    string Name,
    int MinItems,
    int MaxItems,
    Guid? FromCategoryId,
    bool AllowDuplicates,
    int SortOrder,
    IReadOnlyList<BundleSlotOptionDto> Options);

public record ProductCategoryDto(Guid Id, string Slug, string Name, Guid? ParentCategoryId, int SortOrder);

/// <summary>Full product detail, including variants, media and (for bundles) selection slots.</summary>
public record ProductDto(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string Status,
    string Kind,
    Guid? CategoryId,
    string TagsJson,
    string AttributesJson,
    string? BundlePricingMode,
    decimal? BundleFixedAmount,
    decimal? BundlePremium,
    string? BundleCurrency,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductMediaDto> Media,
    IReadOnlyList<BundleSlotDto> BundleSlots);

/// <summary>Lightweight product row for list/browse responses.</summary>
public record ProductSummaryDto(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    string Kind,
    Guid? CategoryId,
    int VariantCount);
