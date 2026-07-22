using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// A saleable catalog product (Spec 042 §8). Anemic. For a composite / build-your-own-box product
/// (<see cref="Kind"/> = <see cref="ProductKinds.Bundle"/>) the bundle's selection slots live in
/// <see cref="BundleSlot"/> and the box-level pricing is described by the <c>Bundle*</c> fields (§12).
/// </summary>
public class Product : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = ProductStatuses.Draft;

    /// <summary>Simple | Variant | Bundle. See <see cref="ProductKinds"/>.</summary>
    public string Kind { get; set; } = ProductKinds.Simple;

    public Guid? CategoryId { get; set; }
    public string TagsJson { get; set; } = "[]";
    public string AttributesJson { get; set; } = "{}";

    // Spec 042 §12 — bundle (build-your-own-box) pricing. Null for non-bundle products.
    public string? BundlePricingMode { get; set; }
    public decimal? BundleFixedAmount { get; set; }
    public decimal? BundlePremium { get; set; }
    public string? BundleCurrency { get; set; }

    /// <summary>Spec 057 §10 — target gross-margin percentage (0–100) the margin report measures
    /// achieved margin against. Null = no target set (the report never flags the product).</summary>
    public decimal? TargetMarginPct { get; set; }

    /// <summary>Spec 066 §5 — per-unit surcharge added on top of container/box pricing (a
    /// "signature"-style upgrade). Null = none. Independent of any personalisation adjustment:
    /// both can apply to the same unit. Display labelling is tenant configuration, not platform.</summary>
    public decimal? UnitSurcharge { get; set; }

    /// <summary>ISO currency of <see cref="UnitSurcharge"/>; required whenever the surcharge is
    /// set. A bare amount would be silently reinterpreted if the storefront currency changed, so
    /// rule V10 checks this against the requested quote currency like any option group.</summary>
    public string? UnitSurchargeCurrency { get; set; }

    /// <summary>Spec 070 §7 — JSON array of hidden search keywords ("goat", "shaki", "party").
    /// Matched by catalog search, serialized into NO public DTO, ever — a dedicated column
    /// precisely so it cannot leak through the <see cref="AttributesJson"/> pass-through.
    /// Editable per product without a release.</summary>
    public string SearchKeywordsJson { get; set; } = "[]";

    public List<ProductVariant> Variants { get; set; } = new();
    public List<ProductMedia> Media { get; set; } = new();
    public List<BundleSlot> BundleSlots { get; set; } = new();
}
