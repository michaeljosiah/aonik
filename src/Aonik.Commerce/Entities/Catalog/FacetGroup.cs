using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A storefront filter group with declared match semantics (Spec 070 §5/§6). The front end renders
/// whatever this returns; adding, renaming, reordering or retiring groups is data, not code
/// (Step 2 FR-3.1).
/// </summary>
public class FacetGroup : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Stable request key: "wellness", "calories". Never renamed once live.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display label: "Wellness goal". Free to change without breaking requests.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Category | Tag | Attribute | Range — see <see cref="FacetMatchKinds"/> (§6).</summary>
    public string MatchKind { get; set; } = FacetMatchKinds.Tag;

    /// <summary>For Attribute/Range: the <c>AttributesJson</c> property read, e.g. "spice" or
    /// "nutrition.kcal". Null for Category and Tag (and rejected if supplied).</summary>
    public string? SourcePath { get; set; }

    /// <summary>Ordered options. Every option — all four kinds, ranges included — carries a stable
    /// <c>value</c> token (the request key, never renamed) plus a mutable display <c>label</c>.
    /// Category/Tag/Attribute: <c>[{ "value": "...", "label": "..." }]</c>.
    /// Range: <c>[{ "value": "under-500", "label": "Under 500 kcal", "min": null, "max": 500 }]</c>
    /// — half-open bands, min inclusive, max exclusive. Clients submit values; labels are free to
    /// change without breaking requests.</summary>
    public string OptionsJson { get; set; } = "[]";

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>Known <see cref="FacetGroup.MatchKind"/> values (Spec 070 §6).</summary>
public static class FacetMatchKinds
{
    /// <summary>Product's category (or an ancestor) is one of the selected category slugs.</summary>
    public const string Category = "Category";

    /// <summary>TagsJson contains any selected value.</summary>
    public const string Tag = "Tag";

    /// <summary>The AttributesJson value at SourcePath equals any selected value (string compare).</summary>
    public const string Attribute = "Attribute";

    /// <summary>The numeric AttributesJson value at SourcePath falls in any selected [min, max)
    /// band; a product missing the value matches no band.</summary>
    public const string Range = "Range";

    public static bool IsKnown(string? value) => value is Category or Tag or Attribute or Range;
}
