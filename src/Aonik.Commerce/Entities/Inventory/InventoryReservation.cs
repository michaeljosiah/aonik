using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Inventory;

/// <summary>
/// A hold on stock for a single stock item — a component variant taken at checkout (Spec 042 §10)
/// or a raw ingredient (Spec 052 §8). Reserve-before-order; commit on payment capture; release on
/// cancellation or TTL expiry. Exactly one of <see cref="ProductVariantId"/> /
/// <see cref="IngredientId"/> is set, agreeing with <see cref="StockItemKind"/>, enforced by a DB
/// CHECK. Anemic.
/// </summary>
public class InventoryReservation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Set iff this hold is on a finished-goods variant (Spec 052 §8).</summary>
    public Guid? ProductVariantId { get; set; }

    /// <summary>Set iff this hold is on a raw material (Spec 052 §8).</summary>
    public Guid? IngredientId { get; set; }

    /// <summary>Discriminator over what this hold holds; see <see cref="StockItemKinds"/>.</summary>
    public string StockItemKind { get; set; } = StockItemKinds.ProductVariant;

    /// <summary>
    /// The holder the reservation was taken for — the cart at checkout (the unit of checkout,
    /// Spec 042 §10). Nullable because a production hold has no cart (Spec 052 §8). Maps onto the
    /// legacy <c>CartId</c> column so existing reservation rows keep their holder.
    /// </summary>
    public Guid? HoldRef { get; set; }

    public decimal Quantity { get; set; }
    public string Status { get; set; } = InventoryReservationStatuses.Held;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Known values for <see cref="InventoryReservation.Status"/>.</summary>
public static class InventoryReservationStatuses
{
    public const string Held = "Held";
    public const string Committed = "Committed";
    public const string Released = "Released";
}
