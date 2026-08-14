using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Usage;

/// <summary>
/// How many slots of a ceiling meter a subscriber currently holds (Spec 087 §9.1).
///
/// A ceiling is checked and claimed, never consumed — deleting the held object returns the slot,
/// which is the behaviour that distinguishes it from a counter. The aggregate is updated by
/// compare-and-increment under <c>RowVersion</c>, so two callers at the limit cannot both succeed.
/// </summary>
public class CeilingHolding : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string SubscriberKind { get; set; } = string.Empty;

    public Guid SubscriberId { get; set; }

    public string MeterCode { get; set; } = string.Empty;

    /// <summary>
    /// Slots currently claimed — <strong><c>long</c>, not <c>int</c></strong> (Spec 089 §9.1).
    ///
    /// <para>
    /// Every ceiling to date has counted seats and profiles, where <c>int</c> was ample. A byte ceiling is not
    /// close: <c>int.MaxValue</c> is 2,147,483,647 and a 200GB allowance is 214,748,364,800 — exactly one
    /// hundred times larger, and even a 3GB world overflows it. A wrapped aggregate does not fail loudly; it
    /// under-counts, so the ceiling stops refusing and the storage bill is discovered later.
    /// </para>
    /// </summary>
    public long Held { get; set; }

    public List<CeilingClaim> Claims { get; set; } = new();
}
