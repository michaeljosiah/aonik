using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// One received ingredient on a <see cref="GoodsReceipt"/> (Spec 054 §7).
/// <see cref="IngredientId"/> matches the PO line's soft ingredient reference
/// (<c>OrderItem.ProductId</c> = ingredient id, per the Spec 053 line shape);
/// <see cref="QuantityReceived"/> is in the ingredient's <c>BaseUnit</c> (Spec 050). A non-null
/// <see cref="UnitCostActual"/> (per base unit) also drives the Spec 051 cost refresh — a new
/// effective-dated <c>IngredientCost</c> row in <see cref="Currency"/> (stamped from the PO's
/// <c>CurrencyIn</c>; null when the line carries no cost). Anemic.
/// </summary>
public class GoodsReceiptLine : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid GoodsReceiptId { get; set; }
    public Guid IngredientId { get; set; }

    /// <summary>Quantity received, in the ingredient's <c>BaseUnit</c> (Spec 050).</summary>
    public decimal QuantityReceived { get; set; }

    /// <summary>Actual ex-supplier unit cost paid, per base unit; null ⇒ stock only, no cost refresh (§10).</summary>
    public decimal? UnitCostActual { get; set; }

    /// <summary>ISO 4217 currency of <see cref="UnitCostActual"/> — the PO's <c>CurrencyIn</c>
    /// (Spec 051's <c>SetCostAsync</c> requires one); null when no cost is carried.</summary>
    public string? Currency { get; set; }
}
