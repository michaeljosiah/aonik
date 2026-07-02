using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Inventory;

/// <summary>
/// Stock for a single stock item — a saleable <c>ProductVariant</c> (Spec 042 §10) or a raw
/// <c>Ingredient</c> (Spec 052 §8) — at an optional location. Available = OnHand - Reserved.
/// Exactly one of <see cref="ProductVariantId"/> / <see cref="IngredientId"/> is set, agreeing
/// with <see cref="StockItemKind"/>, enforced by a DB CHECK. A bundle product holds no stock of
/// its own; only component variants do. Anemic.
/// </summary>
public class InventoryLevel : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Set iff this level stocks a finished-goods variant (Spec 052 §8).</summary>
    public Guid? ProductVariantId { get; set; }

    /// <summary>Set iff this level stocks a raw material (Spec 052 §8).</summary>
    public Guid? IngredientId { get; set; }

    /// <summary>Discriminator over what this level stocks; see <see cref="StockItemKinds"/>.</summary>
    public string StockItemKind { get; set; } = StockItemKinds.ProductVariant;

    public string? Location { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }

    /// <summary>
    /// Raise a low-stock alert when available (OnHand - Reserved) falls to or below this
    /// (Spec 052 §9). Null = no alerting; finished-goods rows leave it null and are never scanned.
    /// </summary>
    public decimal? ReorderPoint { get; set; }

    /// <summary>Optional suggested top-up quantity — stored for Spec 053, not acted on here (Spec 052 §9).</summary>
    public decimal? ReorderQuantity { get; set; }
}
