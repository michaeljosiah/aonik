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

    /// <summary>Slots currently claimed.</summary>
    public int Held { get; set; }

    public List<CeilingClaim> Claims { get; set; } = new();
}
