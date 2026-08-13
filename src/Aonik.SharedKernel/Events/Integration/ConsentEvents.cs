namespace Aonik.SharedKernel.Events.Integration;

// ── Age-transition events (Spec 095 §11) ─────────────────────────────────────
// The platform decides WHEN a transition happens and applies its data effects. It
// does not decide how anyone is told about it: notice copy, the account offer and
// the suspension experience are product decisions, and Arke Kids will want them to
// read very differently from anything Payabo would send.
//
// So the platform emits and the product subscribes. That also keeps the notice
// requirement honest — §11 says both parties are notified IN ADVANCE, which is a
// separate event from the transition itself precisely because it fires earlier.

/// <summary>
/// Raised ahead of a transition so the product can give notice (Spec 095 §11), which is what makes
/// the change expected rather than abrupt.
/// </summary>
/// <param name="Transition">
/// Which boundary is approaching: <c>consent-age</c> or <c>majority</c>. They are different events
/// with different consequences and must not be collapsed.
/// </param>
public record AgeTransitionApproachingEvent(
    Guid TenantId,
    Guid SubjectPartyId,
    IReadOnlyList<Guid> GuardianPartyIds,
    string Transition,
    DateTime OccursOn) : IIntegrationEvent;

/// <summary>
/// Raised when a party reaches their jurisdiction's consent age (Spec 095 §11.2).
///
/// <para>
/// Guardian <em>consents</em> have lapsed; the <c>Guardian</c> edge is <strong>still active</strong>
/// and remains so until majority. The product's job is to offer the young person their own account,
/// and to suspend rather than delete if they never claim it.
/// </para>
/// </summary>
public record ConsentAgeReachedEvent(
    Guid TenantId,
    Guid SubjectPartyId,
    IReadOnlyList<string> LapsedPurposes) : IIntegrationEvent;

/// <summary>
/// Raised when a party reaches majority (Spec 095 §11.1). All guardian authority has ended.
/// </summary>
public record MajorityReachedEvent(
    Guid TenantId,
    Guid SubjectPartyId,
    IReadOnlyList<Guid> FormerGuardianPartyIds) : IIntegrationEvent;

/// <summary>
/// Raised when a party moves safety band (Spec 096 §9) — what may be generated for them, and how
/// strictly, has changed. Separate from the consent transitions because safety banding tracks
/// minority rather than consent capacity, and the two change on different dates.
/// </summary>
public record SafetyBandChangedEvent(
    Guid TenantId,
    Guid SubjectPartyId,
    string PreviousBand,
    string NewBand) : IIntegrationEvent;

/// <summary>Known values for <see cref="AgeTransitionApproachingEvent.Transition"/>.</summary>
public static class AgeTransitionKinds
{
    public const string ConsentAge = "consent-age";
    public const string Majority = "majority";
}
