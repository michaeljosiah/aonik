using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Production;

/// <summary>
/// A production run / work order (Spec 056 §7): the dishes to make (its
/// <see cref="ProductionOrderLine"/>s) and a guarded lifecycle from Planned to Completed. The two
/// stock-moving edges live in <c>IProductionOrderService</c>: RELEASE consumes ingredient stock —
/// the frozen per-line recipe snapshots fanned out over Spec 052 ingredient levels in one
/// all-or-nothing commit (§9) — and COMPLETE optionally yields finished-good stock (§10). This is
/// an internal work order, deliberately distinct from a customer Order on the Spec 041 spine: it
/// moves inventory, never money. Anemic.
/// </summary>
public class ProductionOrder : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>When this run is scheduled to be made (UTC).</summary>
    public DateTime PlannedFor { get; set; }

    /// <summary>See <see cref="ProductionOrderStatuses"/>; driven only through the service's guarded transitions (§8).</summary>
    public string Status { get; set; } = ProductionOrderStatuses.Planned;

    public string? Notes { get; set; }

    /// <summary>When the run was released — the instant ingredient stock was consumed (§9); null until then.</summary>
    public DateTime? ReleasedAt { get; set; }

    /// <summary>When the run was completed (§10); null until then.</summary>
    public DateTime? CompletedAt { get; set; }

    public List<ProductionOrderLine> Lines { get; set; } = new();
}

/// <summary>
/// Known values for <see cref="ProductionOrder.Status"/> (Spec 056 §8) — an open string with a
/// known-values helper, mirroring the platform's open-enum convention. Stock is consumed exactly
/// once, on the Planned → Released edge; Completed and Cancelled are terminal. InProgress is an
/// optional operational sub-state (the kitchen is cooking) carrying no stock effect.
/// </summary>
public static class ProductionOrderStatuses
{
    public const string Planned = "Planned";
    public const string Released = "Released";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}
