namespace Aonik.Commerce.Entities.Catalog;

/// <summary>
/// Known values for <see cref="Product.Kind"/> (Spec 042 §8/§12). An open string on the entity
/// so new kinds are additive; this is the known-values helper.
/// </summary>
public static class ProductKinds
{
    /// <summary>A single saleable product with one implicit variant.</summary>
    public const string Simple = "Simple";

    /// <summary>A product sold in multiple variants (size/flavour) under one listing.</summary>
    public const string Variant = "Variant";

    /// <summary>A composite / build-your-own-box product assembled from component variants (§12).</summary>
    public const string Bundle = "Bundle";
}

/// <summary>Known values for <see cref="Product.Status"/>.</summary>
public static class ProductStatuses
{
    public const string Draft = "Draft";
    public const string Active = "Active";
    public const string Archived = "Archived";
}

/// <summary>
/// Known pricing modes for a <see cref="ProductKinds.Bundle"/> product (Spec 042 §12). Selects how
/// <c>IProductPricingService.ResolveBundlePrice</c> computes a box's line price.
/// </summary>
public static class BundlePricingModes
{
    /// <summary>One fixed box price regardless of contents (e.g. "any 6 for ₦12,000").</summary>
    public const string Fixed = "Fixed";

    /// <summary>The sum of the chosen components' prices.</summary>
    public const string SumOfComponents = "SumOfComponents";

    /// <summary>The sum of the chosen components' prices plus a fixed box premium.</summary>
    public const string SumPlusPremium = "SumPlusPremium";

    /// <summary>Priced by box size via a <c>BundleSizePlan</c> — presets override a linear formula
    /// (Spec 068 §5). Authoring a size plan requires this mode.</summary>
    public const string SizeTiered = "SizeTiered";
}
