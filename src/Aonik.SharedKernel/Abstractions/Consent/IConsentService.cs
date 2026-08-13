namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// The write side of consent (Spec 095 §14).
///
/// <para>
/// It is also the <strong>only</strong> service permitted to create a <c>Guardian</c> relationship.
/// The generic party API refuses that code outright, because every other relationship code merely
/// describes while this one authorises — and the generic path is fed caller-supplied codes by
/// ordinary finance workflows (§7.2).
/// </para>
/// </summary>
public interface IConsentService
{
    /// <summary>
    /// Creates a child, their first guardian edge and their initial consents in
    /// <strong>one transaction</strong> (Spec 095 §12.2).
    ///
    /// <para>
    /// Gated on the <em>guardian</em> being verified, not on a consent — because the child does not
    /// exist until this call succeeds, so no grant naming them can possibly exist beforehand. Gating
    /// enrolment on <c>service-core</c> was a deadlock: the check could never pass.
    /// </para>
    ///
    /// <para>
    /// There is deliberately no general "create child party" endpoint. Its absence is what keeps the
    /// gate meaningful — a caller cannot create a child now and consent later.
    /// </para>
    /// </summary>
    /// <exception cref="GuardianVerificationFailedException">
    /// The guardian could not be verified, or no accepted method was available. Nothing is written.
    /// </exception>
    Task<EnrolChildResult> EnrolChildAsync(
        EnrolChildRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a further guardian to an <strong>existing</strong> child (Spec 095 §7.2).
    ///
    /// <para>
    /// Without this, multiplicity would be unreachable: enrolment creates a <em>new</em> child, so a
    /// second guardian could only be added by enrolling a duplicate. And multiplicity is
    /// load-bearing — withdrawal-wins means nothing with one guardian.
    /// </para>
    ///
    /// <para>The new guardian is verified to the same standard, and an <em>existing</em> active
    /// guardian must authorise the addition, so authority cannot be granted to oneself.</para>
    /// </summary>
    Task AddGuardianAsync(
        AddGuardianRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Grants a purpose. Granting a new <c>TermsVersion</c> <strong>atomically revokes the prior
    /// active grant</strong> for the same subject and purpose, so two active versions can never
    /// coexist — which is what makes a material terms change actually invalidate something.
    /// </summary>
    Task GrantAsync(GrantConsentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a purpose. <strong>Any single active guardian may withdraw, and withdrawal takes
    /// immediate effect regardless of the others</strong> (Spec 095 §7.1).
    ///
    /// <para>
    /// Asymmetric on purpose: processing a child's data over an objection from someone with legal
    /// authority is the worse error. The platform is not equipped to adjudicate family disputes and
    /// must not try — it takes the conservative branch and leaves the dispute to the people who can
    /// actually resolve it.
    /// </para>
    /// </summary>
    Task WithdrawAsync(WithdrawConsentRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a new current terms version, <strong>revoking every affected active grant at
    /// publication</strong> rather than waiting for a replacement (Spec 095 §10.2).
    ///
    /// <para>
    /// Waiting would mean a guardian who never replies goes on authorising processing under terms
    /// this specification calls invalid. The consequence is deliberate and is the point: publishing a
    /// material change <strong>stops processing for every affected family until they re-consent</strong>.
    /// Making that cheap would make consent meaningless.
    /// </para>
    /// </summary>
    /// <returns>How many grants were revoked, so the caller can size the re-consent campaign.</returns>
    Task<int> PublishTermsVersionAsync(
        PublishTermsRequest request,
        CancellationToken cancellationToken = default);
}

/// <param name="GuardianPartyId">The adult enrolling the child. Must be verifiable.</param>
/// <param name="ChildDisplayName">What the child is called in the product.</param>
/// <param name="ChildDateOfBirth">
/// Attested by the guardian. Used to compute the boundary dates and <strong>then discarded</strong>
/// — it is never persisted. Required: there is no fallback (Spec 095 §6).
/// </param>
/// <param name="Jurisdiction">Resolved from the guardian's residence or billing data, never from an IP.</param>
/// <param name="TermsVersion">Which terms text the guardian agreed to.</param>
/// <param name="Purposes">
/// Purposes granted at enrolment. <c>service-core</c> is always included; everything else defaults to
/// NOT granted, per the Children's Code high-privacy-by-default standard.
/// </param>
public sealed record EnrolChildRequest(
    Guid GuardianPartyId,
    string ChildDisplayName,
    DateOnly ChildDateOfBirth,
    string? Jurisdiction,
    string TermsVersion,
    IReadOnlyList<string> Purposes);

/// <param name="EnrolmentAttemptId">
/// Minted before verification and correlating the verification record — which commits outside this
/// transaction — to the child created by it.
/// </param>
public sealed record EnrolChildResult(
    Guid ChildPartyId,
    Guid EnrolmentAttemptId,
    string VerificationMethod,
    DateTime ConsentAgeOn,
    DateTime MajorityOn,
    string SafetyBand);

public sealed record AddGuardianRequest(
    Guid ChildPartyId,
    Guid NewGuardianPartyId,
    // An existing active guardian, authorising the addition. Never the new guardian.
    Guid AuthorisedByPartyId,
    string? Jurisdiction);

public sealed record GrantConsentRequest(
    Guid SubjectPartyId,
    Guid GrantedByPartyId,
    string Purpose,
    string TermsVersion,
    string? Jurisdiction,
    string VerificationMethod,
    string? VerificationRef);

public sealed record WithdrawConsentRequest(
    Guid SubjectPartyId,
    Guid WithdrawnByPartyId,
    string Purpose);

public sealed record PublishTermsRequest(
    string TermsVersion,
    // Which purposes the change is material to. Others keep their grants.
    IReadOnlyList<string> AffectedPurposes);

/// <summary>Thrown when enrolment cannot verify the guardian. Nothing is written.</summary>
public sealed class GuardianVerificationFailedException : Exception
{
    public GuardianVerificationFailedException(Guid guardianPartyId, string reason)
        : base($"Guardian {guardianPartyId} could not be verified: {reason}")
    {
        GuardianPartyId = guardianPartyId;
    }

    public Guid GuardianPartyId { get; }
}
