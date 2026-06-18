using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Inventory;

/// <summary>
/// A hold on stock for a component variant taken at checkout (Spec 042 §10). Reserve-before-order;
/// commit on payment capture; release on cancellation or TTL expiry. Anemic.
/// </summary>
public class InventoryReservation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ProductVariantId { get; set; }

    /// <summary>The cart the reservation was taken for (the unit of checkout).</summary>
    public Guid CartId { get; set; }

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
