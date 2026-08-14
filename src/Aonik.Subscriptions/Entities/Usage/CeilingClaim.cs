using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Usage;

/// <summary>
/// One held object's claim on a ceiling slot (Spec 087 §9.1).
///
/// The aggregate counter alone cannot be idempotent: a retried create would increment twice and
/// permanently lose a slot, and a retried delete would decrement twice and admit more objects than
/// the ceiling. Sharing a transaction with the object's creation makes each individual call atomic
/// — it does not tell the <em>next</em> call that this holder already claimed. This row does.
/// </summary>
public class CeilingClaim : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid CeilingHoldingId { get; set; }

    /// <summary>The stable identity of the object occupying the slot — e.g. a child profile id.</summary>
    public string HolderRef { get; set; } = string.Empty;

    /// <summary>
    /// How much of the ceiling this holder occupies (Spec 089 §9.1).
    ///
    /// <para>
    /// One for a seat or a profile; the blob's size for a byte ceiling. Persisted rather than recomputed so
    /// release returns exactly what the claim took — recomputing at release time would read the object's size
    /// after it had changed, or after it was gone.
    /// </para>
    /// </summary>
    public long Weight { get; set; } = 1;

    public DateTime ClaimedAt { get; set; }
}
