namespace Aonik.Commerce.Contracts.Models.Catalog;

/// <summary>An initial variant supplied when creating a product. Prices are set separately via
/// <c>IProductPricingService</c> to keep pricing in one place (Spec 042 §9).</summary>
public record CreateVariantLine(
    string Sku,
    string Name,
    string? OptionsJson = null,
    decimal? WeightGrams = null);

public record CreateProductCommand(
    string Slug,
    string Name,
    string Kind,
    string Description = "",
    string Status = "Active",
    Guid? CategoryId = null,
    string? TagsJson = null,
    string? AttributesJson = null,
    IReadOnlyCollection<CreateVariantLine>? Variants = null,
    // Bundle (build-your-own-box) pricing — only for Kind = Bundle (§12).
    string? BundlePricingMode = null,
    decimal? BundleFixedAmount = null,
    decimal? BundlePremium = null,
    string? BundleCurrency = null);

public record AddVariantCommand(
    Guid ProductId,
    string Sku,
    string Name,
    string? OptionsJson = null,
    decimal? WeightGrams = null);

public record CreateCategoryCommand(string Slug, string Name, Guid? ParentCategoryId = null, int SortOrder = 0);

public record SetPriceCommand(
    Guid ProductVariantId,
    string Currency,
    decimal Amount,
    DateTime? EffectiveFrom = null,
    DateTime? EffectiveTo = null);

public record AddBundleSlotCommand(
    Guid BundleProductId,
    string Name,
    int MinItems,
    int MaxItems,
    Guid? FromCategoryId = null,
    bool AllowDuplicates = false,
    int SortOrder = 0,
    IReadOnlyCollection<AddBundleSlotOptionLine>? Options = null);

public record AddBundleSlotOptionLine(Guid ProductVariantId, decimal? PriceDelta = null);

/// <summary>One chosen component within a build-your-own-box selection (Spec 042 §12).</summary>
public record BundleSelectionLine(Guid BundleSlotId, Guid ProductVariantId, decimal Quantity = 1m);

public record ListProductsQuery(
    string? Kind = null,
    Guid? CategoryId = null,
    string? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50);
