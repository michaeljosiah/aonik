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
    int PageSize = 50,
    /// Spec 070 §6 — facet key → selected option VALUES (stable tokens, never labels). OR within
    /// a group, AND across groups. Unknown keys or values are rejected loudly, never ignored.
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Facets = null,
    /// Spec 070 §6 — collection slug membership filter.
    string? Collection = null,
    /// Spec 070 §6 — name | newest | rank. Rank requires a collection filter. Null defaults to
    /// rank when a collection is present, name otherwise.
    string? Sort = null);

/// <summary>The missing product update (Spec 070 §10) — PATCH semantics: every member optional,
/// only supplied members apply, JSON fields validated on write (§11). <c>ClearCategory</c> exists
/// because a nullable Guid alone cannot distinguish "unchanged" from "remove the category".</summary>
public record UpdateProductCommand(
    string? Name = null,
    string? Description = null,
    string? Status = null,
    Guid? CategoryId = null,
    bool ClearCategory = false,
    string? TagsJson = null,
    string? AttributesJson = null,
    string? SearchKeywordsJson = null);

public record ProductMediaLine(string Url, string? Kind = null);

/// <summary>Full-replace of a product's ordered media (Spec 070 §10) — list/reorder/remove;
/// upload wiring is out of scope (§3). Null <see cref="Items"/> is rejected: a missing property
/// must never read as an intentional clear.</summary>
public record ReplaceProductMediaCommand(IReadOnlyList<ProductMediaLine>? Items);

/// <summary>Known <see cref="ListProductsQuery.Sort"/> values (Spec 070 §6).</summary>
public static class ProductSortOrders
{
    public const string Name = "name";
    public const string Newest = "newest";
    public const string Rank = "rank";
}
