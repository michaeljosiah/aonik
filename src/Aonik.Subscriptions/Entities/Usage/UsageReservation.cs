using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Usage;

/// <summary>
/// A hold taken before metered work runs (Spec 087 §9) — the same authorise/capture shape the
/// payments module uses, for the same reason: the work may cost less than expected, or not happen.
/// </summary>
public class UsageReservation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string SubscriberKind { get; set; } = string.Empty;

    public Guid SubscriberId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public string MeterCode { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>One of <c>UsageReservationStatuses</c>.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>When the sweep returns the hold if it is neither committed nor released.</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Caller-generated, unique per tenant. Replaying returns the existing reservation rather than taking a second hold.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public List<UsageReservationAllocation> Allocations { get; set; } = new();
}
