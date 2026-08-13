namespace Aonik.SharedKernel.Abstractions.Consent;

/// <summary>
/// The enforcement point for consent (Spec 095 §12.1). Fails closed.
///
/// <para>
/// <strong>Why a gate rather than a reader call at each site.</strong> <see cref="IConsentReader"/>
/// answers a question; a caller is free to ignore the answer, and the failure mode of "someone
/// forgot the check" is silent. Spec 095 §12.1 is explicit that a consent check the <em>product</em>
/// performs is not a control — the same lesson
/// <a href="../../../../docs/specifications/089.workspaces.html">Spec 089 §8.1</a> learned about
/// read/write access, and the reason
/// <a href="../../../../docs/specifications/032.tiered-ai-mutation-approval.html">Spec 032</a> gates
/// tools centrally rather than trusting each one.
/// </para>
///
/// <para>
/// So this interface never returns a boolean. It either proceeds or throws. There is no shape of
/// call site that can accidentally continue.
/// </para>
/// </summary>
public interface IConsentGate
{
    /// <summary>
    /// Proceed only if an active grant covers <paramref name="purpose"/> for this subject.
    /// </summary>
    /// <exception cref="ConsentRequiredException">
    /// No active grant. Thrown, never returned — "log and continue" is not available to a caller.
    /// </exception>
    Task EnsureAsync(
        Guid subjectPartyId,
        string purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Proceed only if the caller holds active guardian authority over the subject, <em>or</em> is
    /// the subject acting for themselves.
    /// </summary>
    Task EnsureCanActForAsync(
        Guid callerPartyId,
        Guid subjectPartyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Proceed only if the consents this <em>route</em> requires are present (Spec 095 §12.3).
    ///
    /// <para>
    /// The route is resolved <strong>before</strong> the check, not after. Gating every generation
    /// on <c>generation-disclosure</c> would refuse a family the privacy-preserving local option
    /// they were declining in favour of — which is close to the opposite of what the purpose is for.
    /// </para>
    /// </summary>
    Task EnsureGenerationAsync(
        Guid subjectPartyId,
        GenerationRoute route,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Where a generation actually executes, and therefore whose consent it needs (Spec 095 §12.3).
/// </summary>
/// <param name="ExecutesRemotely">
/// True when the subject's inputs leave their device to be <em>authored</em> by a third party.
/// </param>
/// <param name="ClassifiesRemotely">
/// True when the output is sent to an external classifier to be <em>judged</em>
/// (<a href="../../../../docs/specifications/096.content-safety-for-child-facing-generation.html">Spec 096</a>).
///
/// <para>
/// Materially less disclosure than remote authoring, and honestly still some — which is why it is a
/// separate purpose rather than folded into the other. A first draft of §12.3 said local generation
/// needed only <c>service-core</c> "because nothing leaves the device", and missed that safety
/// classification is itself egress.
/// </para>
/// </param>
public readonly record struct GenerationRoute(bool ExecutesRemotely, bool ClassifiesRemotely)
{
    /// <summary>Authored and judged externally. The ordinary cloud path.</summary>
    public static readonly GenerationRoute Remote = new(true, true);

    /// <summary>
    /// Authored on the device, judged externally. The best route available today, and the one a
    /// family declining remote authoring should get.
    /// </summary>
    public static readonly GenerationRoute LocalWithRemoteClassification = new(false, true);

    /// <summary>
    /// Authored and judged on the device. <strong>Requires on-device classifiers, which do not exist
    /// yet</strong> — recorded so the shape is right when they do, rather than implying a capability
    /// we have.
    /// </summary>
    public static readonly GenerationRoute FullyLocal = new(false, false);

    /// <summary>
    /// The purposes this route needs, beyond <c>service-core</c>. Ordered for a stable message.
    /// </summary>
    public IReadOnlyList<string> RequiredPurposes()
    {
        var purposes = new List<string>();

        if (ExecutesRemotely)
        {
            purposes.Add(ConsentPurposes.GenerationDisclosure);
        }

        if (ClassifiesRemotely)
        {
            purposes.Add(ConsentPurposes.SafetyClassification);
        }

        return purposes;
    }
}
