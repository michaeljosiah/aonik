using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Usage;

/// <summary>
/// Allowance a subscriber holds (Spec 087 §8). Plan allowance and purchased top-ups are the same
/// shape with different expiry, so they are one entity with a <see cref="Source"/> discriminator
/// rather than two tables that drift.
///
/// <b>Keyed by subscriber, not subscription.</b> A purchased grant never expires, so it must
/// outlive the subscription it was bought under; keyed by subscription, a cancel-and-resubscribe
/// would strand paid units — invisible to draw-down while their deferred-revenue liability stayed
/// on the ledger.
/// </summary>
public class EntitlementGrant : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The grant's owner. One of <c>SubscriberKinds</c>.</summary>
    public string SubscriberKind { get; set; } = string.Empty;

    public Guid SubscriberId { get; set; }

    /// <summary>Provenance on a plan grant, not the ownership key.</summary>
    public Guid? SubscriptionId { get; set; }

    public Guid? PeriodId { get; set; }

    public string MeterCode { get; set; } = string.Empty;

    /// <summary>One of <c>GrantSources</c>.</summary>
    public string Source { get; set; } = string.Empty;

    public decimal Allowance { get; set; }

    public decimal Consumed { get; set; }

    /// <summary>
    /// Reserved but not yet committed. Without this a reservation changes nothing on the grant, so
    /// its <c>RowVersion</c> never moves and two concurrent holds both take the last unit.
    /// Available is <c>Allowance - Consumed - Held</c>.
    /// </summary>
    public decimal Held { get; set; }

    /// <summary>
    /// Derived from the originating entitlement's <c>ResetPolicy</c>, <b>not</b> from
    /// <see cref="Source"/>: a plan entitlement that never resets accumulates across renewals
    /// instead of being discarded at each period end.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>One of <c>GrantStatuses</c>. Gives the expiry sweep a transition it can persist, so breakage is a recorded event rather than an inference.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime? ClosedAt { get; set; }

    /// <summary>Purchases and adjustments trace to their order.</summary>
    public Guid? SourceOrderId { get; set; }
}
