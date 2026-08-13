using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities.Safety;

/// <summary>
/// Content that <em>passed</em> every automated layer and is being held for a guardian to see first
/// (Spec 096 §8).
///
/// <para>
/// The ordering is the whole design. Pre-review sits <strong>after</strong> classification, never
/// before it, so a guardian approving a held item is approving something already judged safe. Placed
/// the other way round, guardian approval would become an unconditional bypass of the entire gate —
/// the same mistake §8's first revision made in giving guardians unconditional release, and the
/// reason automated layers must be able to stand alone.
/// </para>
/// </summary>
public class PendingContentReview : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>The <c>Allowed</c> decision this hold followed. A blocked item never reaches here.</summary>
    public Guid SafetyDecisionId { get; set; }

    public Guid SubjectPartyId { get; set; }
    public string SafetyBand { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;

    /// <summary>Storage key for the generated content. Never the content itself.</summary>
    public string Reference { get; set; } = string.Empty;

    public string State { get; set; } = PreReviewStates.Pending;

    public Guid? DecidedByPartyId { get; set; }
    public DateTime? DecidedAt { get; set; }

    public DateTime HeldAt { get; set; }

    /// <summary>
    /// A hold nobody acts on expires undelivered. It does not time out <em>into</em> delivery: an
    /// unattended queue must not become an approval mechanism, which is what any "auto-approve after
    /// N days" would make it.
    /// </summary>
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// A guardian's explicit choice about pre-review for one child (Spec 096 §8).
///
/// <para>
/// A row exists only where a guardian has <strong>chosen</strong>. Absence means the band default
/// applies, and the youngest band defaults to on — so a child whose preference row was never written
/// still gets pre-review. Storing the default as a row at provisioning time would mean a provisioning
/// bug silently disables it, which is the wrong way for this to fail.
/// </para>
/// </summary>
public class ChildSafetyPreference : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid SubjectPartyId { get; set; }

    /// <summary>
    /// Whether generated content is held for the guardian before the child sees it. A guardian may
    /// turn this off even for the youngest band — pre-review is reassurance, not the control, and the
    /// automated layers are unaffected either way. Who turned it off is recorded.
    /// </summary>
    public bool PreReviewEnabled { get; set; }

    public Guid SetByPartyId { get; set; }
    public DateTime SetAt { get; set; }
}

public static class PreReviewStates
{
    public const string Pending = "pending";

    /// <summary>Approved by a guardian. Only this state yields a delivery permit.</summary>
    public const string Approved = "approved";

    public const string Declined = "declined";

    /// <summary>Nobody acted in time. Undelivered — expiry is not a quiet approval.</summary>
    public const string Expired = "expired";
}
