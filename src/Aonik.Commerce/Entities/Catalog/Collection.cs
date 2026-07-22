using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A curated, ordered, named set of products for storefront display (Spec 070 §5). Answers "where
/// do we show it?" — the third taxonomy axis, distinct from category ("what is it?") and tags
/// ("what is it like?"). Anemic.
/// </summary>
public class Collection : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;

    /// <summary>Display title: "Carb-conscious".</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional supporting line under the title.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Featured | Curated | Custom — see <see cref="CollectionKinds"/>. Presentation hint
    /// only; behaviour is identical across kinds.</summary>
    public string Kind { get; set; } = CollectionKinds.Curated;

    /// <summary>Order among collections (a homepage renders them in this order).</summary>
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public List<CollectionItem> Items { get; set; } = new();
}

/// <summary>Known <see cref="Collection.Kind"/> values. Presentation hints, not behaviour.</summary>
public static class CollectionKinds
{
    public const string Featured = "Featured";
    public const string Curated = "Curated";
    public const string Custom = "Custom";

    public static bool IsKnown(string? value) => value is Featured or Curated or Custom;
}
