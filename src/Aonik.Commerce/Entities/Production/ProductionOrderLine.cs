using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Production;

/// <summary>
/// One dish of a production run (Spec 056 §7): the Spec 042 <c>ProductVariant</c> to produce and
/// how many portions. <see cref="RecipeSnapshotJson"/> is the line's frozen PER-PORTION component
/// bill — serialized <c>RecipeSnapshotComponent</c>s from Spec 050's <c>ExplodeAsync(variant, 1)</c>,
/// captured at CREATION — and is what BOTH the kitchen sheet (§11) and release consumption (§9)
/// read, so a recipe edited between print and release can never make them diverge (the per-line
/// stand-in for Spec 050's recipe-versioning open decision). A variant with no active recipe is
/// rejected at creation, so a line never carries an empty snapshot. Anemic.
/// </summary>
public class ProductionOrderLine : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ProductionOrderId { get; set; }

    /// <summary>The finished Spec 042 variant being produced — the variant its Spec 050 recipe attaches to.</summary>
    public Guid ProductVariantId { get; set; }

    /// <summary>Portions to produce; what release consumption is computed against (§9).</summary>
    public decimal PlannedQuantity { get; set; }

    /// <summary>Actual portions made; recorded at completion (defaults to planned), null until then (§10).</summary>
    public decimal? ProducedQuantity { get; set; }

    /// <summary>The frozen per-portion component bill (see class remarks). Required — never empty.</summary>
    public string RecipeSnapshotJson { get; set; } = string.Empty;
}
