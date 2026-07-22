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

/// <summary>Carries <see cref="IsActive"/> so the back office can see — and therefore reactivate —
/// a retired category (Spec 070 A17); the public surface uses the active-only tree instead.</summary>
public record ProductCategoryDto(Guid Id, string Slug, string Name, Guid? ParentCategoryId, int SortOrder, bool IsActive);

/// <summary>Full product detail, including variants, media, (for bundles) selection slots, the
/// Spec 057 target gross-margin percentage (null = no target set), and the Spec 066 personalisation
/// surface.</summary>
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
    decimal? TargetMarginPct,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductMediaDto> Media,
    IReadOnlyList<BundleSlotDto> BundleSlots,
    /// Spec 066 — what this product actually offers, defaults resolved. An EMPTY list means the
    /// product is not personalisable, and storefronts hide the panel entirely rather than
    /// rendering an empty one.
    IReadOnlyList<EffectiveOptionGroupDto> EffectiveOptionGroups,
    /// Spec 066 — per-unit surcharge and its denomination; null when the product has none.
    decimal? UnitSurcharge,
    string? UnitSurchargeCurrency,
    /// Spec 067 §8 — the RESOLVED standard-preparation content (the §5 resolution of the empty
    /// selection, carrying its flags), not the raw block: after a default move the variant that
    /// now describes the standard combination must win, and a suspect block must arrive
    /// stale-flagged with declarations withheld. Null when no default block is authored, and on
    /// surfaces that do not compose content (admin detail; content has its own admin reads).
    ResolvedContentDto? Content = null,
    /// Spec 067 §8 — the cache-key version the storefront passes back as `v`. Null with Content.
    int? ContentVersion = null);

/// <summary>The ADMIN product detail (Spec 070 §7): every <see cref="ProductDto"/> field plus the
/// hidden search keywords, serialized flat. A distinct type on purpose — the public product read
/// returns <see cref="ProductDto"/>, which structurally cannot leak keywords, and an editor who
/// could not see current keywords would silently erase them on a full update.</summary>
public record AdminProductDetailDto(
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
    decimal? TargetMarginPct,
    IReadOnlyList<ProductVariantDto> Variants,
    IReadOnlyList<ProductMediaDto> Media,
    IReadOnlyList<BundleSlotDto> BundleSlots,
    IReadOnlyList<EffectiveOptionGroupDto> EffectiveOptionGroups,
    decimal? UnitSurcharge,
    string? UnitSurchargeCurrency,
    IReadOnlyList<string> SearchKeywords)
    : ProductDto(
        Id, Slug, Name, Description, Status, Kind, CategoryId, TagsJson, AttributesJson,
        BundlePricingMode, BundleFixedAmount, BundlePremium, BundleCurrency, TargetMarginPct,
        Variants, Media, BundleSlots, EffectiveOptionGroups, UnitSurcharge, UnitSurchargeCurrency);

/// <summary>Lightweight product row for list/browse responses — everything a menu-grid card
/// renders without a detail call (Spec 070 §8). Deliberately carries NO retail price: the brand
/// rule "dishes never show a standalone price" is only possible if the API doesn't force prices
/// into every list payload. <see cref="UnitSurcharge"/> is the one price-like field allowed — an
/// on-top-of-the-box delta, not a dish price.</summary>
public record ProductSummaryDto(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    string Kind,
    Guid? CategoryId,
    int VariantCount,
    /// First ProductMedia image by SortOrder; null when the product has none.
    string? HeroImageUrl,
    /// Parsed from TagsJson; a malformed legacy row renders with empty tags rather than failing.
    IReadOnlyList<string> Tags,
    /// Pass-through for card badges (spice, etc.).
    string AttributesJson,
    decimal? UnitSurcharge);
