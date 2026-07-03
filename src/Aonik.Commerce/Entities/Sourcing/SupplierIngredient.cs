using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// The supplier catalog / price list (Spec 053 §8/§9): one row per (supplier, ingredient) the
/// supplier sells us, carrying the supplier's SKU, the pack we buy in, and the pack price. This is
/// the buy-side unit conversion promised by Spec 050 §10: <see cref="PackSize"/> is how many of
/// the ingredient's <c>BaseUnit</c> one purchasable pack contains (a 25 kg sack → PackSize 25 for
/// a kg-based ingredient), so <c>PackPrice / PackSize</c> is the per-base-unit cost. A single,
/// explicit per-row factor — not a general unit-of-measure engine. Anemic.
/// </summary>
public class SupplierIngredient : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Intra-module reference to the <see cref="Supplier"/> this price-list row belongs to.</summary>
    public Guid SupplierId { get; set; }

    /// <summary>Intra-module reference to the Spec 050 <see cref="Ingredient"/> this row prices.</summary>
    public Guid IngredientId { get; set; }

    /// <summary>The supplier's SKU / order code for this ingredient.</summary>
    public string? Sku { get; set; }

    /// <summary>Quantity of the ingredient's <c>BaseUnit</c> one purchasable pack contains (e.g. 25 for a 25 kg sack).</summary>
    public decimal PackSize { get; set; }

    /// <summary>Price for one pack, in <see cref="Currency"/>.</summary>
    public decimal PackPrice { get; set; }

    /// <summary>ISO 4217 currency code (defaults to the supplier's currency on upsert).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Per-line lead time in days; overrides <see cref="Supplier.LeadTimeDays"/> for this ingredient.</summary>
    public int? LeadTimeDays { get; set; }
}
