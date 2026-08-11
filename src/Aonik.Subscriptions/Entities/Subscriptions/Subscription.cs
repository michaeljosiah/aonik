using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Subscriptions;

/// <summary>
/// What a subscriber is on, and when it renews (Spec 087 §7).
///
/// The subscriber is a <c>(Kind, Id)</c> pair the module stores and never dereferences — a family
/// group, a party, or the tenant itself. Existence and authority are established per kind through
/// <c>ISubscriberAuthorizer</c>, because tenant scoping alone does not authorise a caller to act
/// for a particular subscriber.
/// </summary>
public class Subscription : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>One of <c>SubscriberKinds</c>.</summary>
    public string SubscriberKind { get; set; } = string.Empty;

    public Guid SubscriberId { get; set; }

    /// <summary>
    /// The pinned version. Pinning is what makes grandfathering real: a later price rise mints a
    /// new version and leaves this subscriber on the one they agreed to.
    /// </summary>
    public Guid PlanVersionId { get; set; }

    /// <summary>
    /// A plan change accepted but not yet in force. Applied only when the next period
    /// <b>settles</b> — an unpaid upgrade must confer nothing, and the current version has to stay
    /// readable until it is paid for.
    /// </summary>
    public Guid? PendingPlanVersionId { get; set; }

    public DateTime? PendingEffectiveAt { get; set; }

    /// <summary>One of <c>SubscriptionStatuses</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CurrentPeriodStart { get; set; }

    public DateTime CurrentPeriodEnd { get; set; }

    /// <summary>
    /// When true the renewal job <b>closes</b> the subscription at the boundary instead of billing
    /// again. Checked before a period is created — selecting on status alone would leave a
    /// cancelled subscription matching the renewal query forever.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>The stored mandate renewals charge (Spec 088 §6). Null for a zero-price plan, which needs no funding.</summary>
    public Guid? PaymentMandateId { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public List<SubscriptionPeriod> Periods { get; set; } = new();
}
