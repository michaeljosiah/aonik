using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// An effective-dated, audited unit cost for an <see cref="Ingredient"/> (Spec 051 §7/§8),
/// mirroring the retail <c>ProductPrice</c> shape and its close-prior/open-new write semantics.
/// <see cref="UnitCost"/> is quoted per the ingredient's <c>BaseUnit</c> (e.g. ₦/kg). Setting a
/// new cost closes the prior open row and inserts a new one — or, when it lands before a
/// scheduled row, splits the window containing it and inserts the new row already closed at the
/// successor's start (the scheduled row stays the single open row) — so "a supplier repriced" is a
/// historied action, never an in-place overwrite. Which row is <em>current</em> on a date is
/// resolved by the <see cref="EffectiveFrom"/>/<see cref="EffectiveTo"/> window (date-aware, §8)
/// — <see cref="IsActive"/> is a convenience/soft-delete flag, NOT the current-selector. Anemic.
/// </summary>
public class IngredientCost : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Intra-module reference to the Spec 050 <see cref="Ingredient"/> this cost prices.</summary>
    public Guid IngredientId { get; set; }

    /// <summary>ISO 4217 currency code (e.g. NGN, GBP). One rollup call is single-currency (Spec 051 §10).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Cost per the ingredient's <c>BaseUnit</c> (e.g. cost per kg for a kg-based ingredient).</summary>
    public decimal UnitCost { get; set; }

    /// <summary>When this cost takes effect (UTC). Required — <c>SetCostAsync</c> defaults it to
    /// "now"; a future date stores a <em>scheduled</em> row that does not price rollups until the
    /// date arrives (Spec 051 §8/R4).</summary>
    public DateTime EffectiveFrom { get; set; }

    /// <summary>When this cost was superseded (UTC, exclusive). Null = the open row. At most one
    /// open row exists per (tenant, ingredient, currency) — DB filtered unique index (Spec 051 §12).</summary>
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Convenience/soft-delete flag kept in sync with "the newest open row" — NOT the
    /// selector for the current cost; the effective dates are (Spec 051 §8).</summary>
    public bool IsActive { get; set; } = true;
}
