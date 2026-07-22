using Aonik.Commerce.Entities.Catalog;

namespace Aonik.Commerce.Contracts.Models.Catalog;

// ─── Collections (Spec 070 §5/§10) ───────────────────────────────────────────

/// <summary>Public read of a collection: Active member products only, in rank order, each in the
/// §8 summary shape a grid card renders. Inactive collections are never served here.</summary>
public record PublicCollectionDto(
    Guid Id,
    string Slug,
    string Title,
    string? Subtitle,
    string Kind,
    int SortOrder,
    IReadOnlyList<ProductSummaryDto> Products);

/// <summary>Admin list row. <see cref="ItemCount"/> counts every staged member, drafts included —
/// the back office must see what the public read hides (A9).</summary>
public record AdminCollectionSummaryDto(
    Guid Id,
    string Slug,
    string Title,
    string? Subtitle,
    string Kind,
    int SortOrder,
    bool IsActive,
    int ItemCount);

/// <summary>Admin detail: full membership with per-member rank and status, drafts included.</summary>
public record AdminCollectionDto(
    Guid Id,
    string Slug,
    string Title,
    string? Subtitle,
    string Kind,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<AdminCollectionItemDto> Items);

public record AdminCollectionItemDto(Guid ProductId, string Slug, string Name, string Status, int Rank);

public record CreateCollectionCommand(
    string Slug,
    string Title,
    string? Subtitle = null,
    string Kind = CollectionKinds.Curated,
    int SortOrder = 0);

/// <summary>Null-valued members preserve the stored value — omission must never be able to
/// deactivate a collection or reorder the homepage as a side effect of a rename.</summary>
public record UpdateCollectionCommand(
    string Title,
    string? Subtitle = null,
    string? Kind = null,
    int? SortOrder = null,
    bool? IsActive = null);

public record CollectionItemLine(Guid ProductId, int Rank);

/// <summary>Full-replace of a collection's ranked membership — idempotent reorder (A12). Null
/// <see cref="Items"/> is rejected by the service: a missing property must never read as an
/// intentional clear, which requires an explicit empty list.</summary>
public record ReplaceCollectionItemsCommand(IReadOnlyList<CollectionItemLine>? Items);

// ─── Facet groups (Spec 070 §5/§6) ───────────────────────────────────────────

/// <summary>A facet group as both surfaces read it. The public endpoint serves active groups only;
/// the storefront renders <see cref="Options"/> verbatim and submits option VALUES back.</summary>
public record FacetGroupDto(
    Guid Id,
    string Key,
    string Label,
    string MatchKind,
    string? SourcePath,
    int SortOrder,
    bool IsActive,
    IReadOnlyList<FacetOptionDto> Options);

/// <summary>One option: stable request <see cref="Value"/>, mutable display <see cref="Label"/>,
/// and — for Range groups — the half-open band [Min, Max), min inclusive, max exclusive.</summary>
public record FacetOptionDto(string Value, string Label, decimal? Min, decimal? Max);

public record CreateFacetGroupCommand(
    string Key,
    string Label,
    string MatchKind,
    string OptionsJson,
    string? SourcePath = null,
    int SortOrder = 0);

/// <summary>Null-valued members preserve the stored value. <c>Key</c> and <c>MatchKind</c> are
/// absent by design: the key is the stable request token, and changing how a live group matches
/// is a retire-and-replace, not an edit.</summary>
public record UpdateFacetGroupCommand(
    string Label,
    string? OptionsJson = null,
    string? SourcePath = null,
    int? SortOrder = null,
    bool? IsActive = null);

// ─── Category tree (Spec 070 §10) ────────────────────────────────────────────

/// <summary>One node of the public category tree: active categories only, children sorted.</summary>
public record CategoryTreeNodeDto(
    Guid Id,
    string Slug,
    string Name,
    int SortOrder,
    IReadOnlyList<CategoryTreeNodeDto> Children);

/// <summary>Null-valued members preserve the stored value; categories retire via
/// <see cref="IsActive"/>, never delete (§11).</summary>
public record UpdateCategoryCommand(
    string Name,
    Guid? ParentCategoryId = null,
    bool ClearParent = false,
    int? SortOrder = null,
    bool? IsActive = null);
