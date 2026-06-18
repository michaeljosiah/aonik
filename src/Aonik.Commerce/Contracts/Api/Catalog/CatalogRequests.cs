namespace Aonik.Commerce.Contracts.Api.Catalog;

/// <summary>HTTP request bodies for the catalog endpoints (Spec 042). Mapped to service commands.</summary>
public record CreateProductRequest(
    string Slug,
    string Name,
    string Kind,
    string? Description,
    string? Status,
    Guid? CategoryId,
    string? TagsJson,
    string? AttributesJson,
    string? BundlePricingMode,
    decimal? BundleFixedAmount,
    decimal? BundlePremium,
    string? BundleCurrency,
    IReadOnlyCollection<CreateVariantRequestLine>? Variants);

public record CreateVariantRequestLine(string Sku, string Name, string? OptionsJson, decimal? WeightGrams);

public record AddVariantRequest(string Sku, string Name, string? OptionsJson, decimal? WeightGrams);

public record SetPriceRequest(string Currency, decimal Amount, DateTime? EffectiveFrom, DateTime? EffectiveTo);

public record CreateCategoryRequest(string Slug, string Name, Guid? ParentCategoryId, int SortOrder);

public record AddBundleSlotRequest(
    string Name,
    int MinItems,
    int MaxItems,
    Guid? FromCategoryId,
    bool AllowDuplicates,
    int SortOrder,
    IReadOnlyCollection<AddBundleSlotOptionRequestLine>? Options);

public record AddBundleSlotOptionRequestLine(Guid ProductVariantId, decimal? PriceDelta);
