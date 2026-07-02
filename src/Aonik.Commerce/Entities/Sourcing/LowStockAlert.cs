using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Sourcing;

/// <summary>
/// The "we're nearly out of rice" signal (Spec 052 §7/§10): raised by the low-stock scan when an
/// ingredient level's available stock falls to or below its reorder point. At most one ACTIVE
/// (<c>Open</c> or <c>Acknowledged</c>) alert exists per (tenant, ingredient) — the scan refreshes
/// the active alert rather than piling up duplicates. <c>Ordered</c>/<c>Resolved</c> are written by
/// the procurement specs (053/054). Anemic.
/// </summary>
public class LowStockAlert : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid IngredientId { get; set; }

    /// <summary>Snapshot of available (OnHand - Reserved) when first raised / last refreshed (Spec 052 §7).</summary>
    public decimal AvailableAtRaise { get; set; }

    /// <summary>Snapshot of the reorder point that fired (Spec 052 §7).</summary>
    public decimal ReorderPoint { get; set; }

    public string Status { get; set; } = LowStockAlertStatuses.Open;
    public DateTime RaisedAt { get; set; }
}

/// <summary>
/// Known values for <see cref="LowStockAlert.Status"/> (Spec 052 §10):
/// Open → Acknowledged → Ordered → Resolved. Open + Acknowledged form the ACTIVE set the scan
/// refreshes; Ordered (Spec 053) and Resolved (Spec 054) end the cycle.
/// </summary>
public static class LowStockAlertStatuses
{
    public const string Open = "Open";
    public const string Acknowledged = "Acknowledged";
    public const string Ordered = "Ordered";
    public const string Resolved = "Resolved";
}
