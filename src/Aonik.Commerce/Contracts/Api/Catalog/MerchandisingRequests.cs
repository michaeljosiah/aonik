namespace Aonik.Commerce.Contracts.Api.Catalog;

/// <summary>HTTP request bodies for the Spec 070 merchandising endpoints. Update requests use
/// nullable value types throughout — an omitted member binds to null and means "leave unchanged",
/// never a destructive CLR default.</summary>
public record CreateCollectionRequest(
    string Slug,
    string Title,
    string? Subtitle = null,
    string? Kind = null,
    int SortOrder = 0);

public record UpdateCollectionRequest(
    string Title,
    string? Subtitle = null,
    bool ClearSubtitle = false,
    string? Kind = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record CollectionItemRequestLine(Guid ProductId, int Rank);

/// <summary>Full-replace membership. <see cref="Items"/> is required: a missing or misspelled
/// property must never be indistinguishable from an intentional clear.</summary>
public record ReplaceCollectionItemsRequest(IReadOnlyList<CollectionItemRequestLine>? Items);

public record CreateFacetGroupRequest(
    string Key,
    string Label,
    string MatchKind,
    string OptionsJson,
    string? SourcePath = null,
    int SortOrder = 0);

public record UpdateFacetGroupRequest(
    string Label,
    string? OptionsJson = null,
    string? SourcePath = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record UpdateCategoryRequest(
    string Name,
    Guid? ParentCategoryId = null,
    bool ClearParent = false,
    int? SortOrder = null,
    bool? IsActive = null);

/// <summary>PATCH semantics for the missing product update (Spec 070 §10): every member is
/// optional; only supplied members apply. JSON fields are validated on write (§11).</summary>
public record UpdateProductRequest(
    string? Name = null,
    string? Description = null,
    string? Status = null,
    Guid? CategoryId = null,
    bool ClearCategory = false,
    string? TagsJson = null,
    string? AttributesJson = null,
    string? SearchKeywordsJson = null);

public record ProductMediaRequestLine(string Url, string? Kind = null);

/// <summary>Full-replace of a product's ordered media. Upload wiring is out of scope (§3);
/// this orders/removes existing URLs. <see cref="Items"/> required for the same reason as
/// collection items — omission must not read as an intentional clear.</summary>
public record ReplaceProductMediaRequest(IReadOnlyList<ProductMediaRequestLine>? Items);

/// <summary>Writes the Commerce.Storefront.* settings behind the §9 config document. Null
/// members leave the stored setting unchanged; explicit values apply.</summary>
public record UpdateStorefrontConfigRequest(
    string? RecommendedChoiceLabel = null,
    int? ResultsPageSize = null,
    string? BackToTopTriggerJson = null,
    decimal? DeliveryListAmount = null,
    decimal? DeliveryChargedAmount = null,
    string? DefaultBoxSlug = null);
