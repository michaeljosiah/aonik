using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Usage;

/// <summary>
/// One grant's share of a hold (Spec 087 §9). A reservation carrying only a total cannot work: a
/// release, an expiry, or a commit at a lower actual quantity would have no record of which grants
/// to restore, and the returned units would land on the wrong ones.
///
/// Rows are ordered by the draw-down rule at reserve time, so a short commit can release from the
/// <b>tail</b> and keep the consumed prefix plan-before-purchase.
/// </summary>
public class UsageReservationAllocation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid ReservationId { get; set; }

    public Guid GrantId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>Position in the draw-down order — 0 is drawn first and released last.</summary>
    public int Ordinal { get; set; }
}
