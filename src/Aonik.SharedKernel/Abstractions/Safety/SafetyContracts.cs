namespace Aonik.SharedKernel.Abstractions.Safety;

/// <summary>
/// Categories a classifier reports (Spec 096 §5). Named plainly, because a spec that will not say
/// what it is defending against is useless to whoever implements the check.
/// </summary>
public static class SafetyCategories
{
    /// <summary>Zero tolerance at every band. No context makes this acceptable output for a child.</summary>
    public const string Sexual = "sexual";

    /// <summary>
    /// The most common real failure, and usually from an <em>innocent</em> prompt — "the knight
    /// fights the dragon" is a normal request from a six-year-old with a wide output distribution.
    /// Banded rather than blanket.
    /// </summary>
    public const string GraphicViolence = "graphic-violence";

    /// <summary>Hardest to classify, because "frightening" is age-relative and not a standard label.</summary>
    public const string Frightening = "frightening";

    /// <summary>Blocked as output, and never used to diagnose the child (Spec 096 §11).</summary>
    public const string SelfHarm = "self-harm";

    public const string Hate = "hate";

    /// <summary>A named classmate or teacher. Easy to overlook: the prompt looks harmless.</summary>
    public const string RealPersonLikeness = "real-person-likeness";

    /// <summary>
    /// The one that is not a judgement call and not ours to moderate. Detection preserves,
    /// overrides deletion, restricts access, and escalates to a person immediately (§12).
    /// </summary>
    public const string Csam = "csam";

    /// <summary>
    /// Categories no guardian may release, and which are not even viewable by them (Spec 096 §8).
    /// A guardian account is not proof of good intent, and this is where that matters most.
    /// </summary>
    public static readonly IReadOnlySet<string> NonOverridable =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Sexual, SelfHarm, Csam };

    /// <summary>Requires immediate preservation and human escalation, overriding retention (§12).</summary>
    public static readonly IReadOnlySet<string> Reportable =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Csam };

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Sexual, GraphicViolence, Frightening, SelfHarm, Hate, RealPersonLikeness, Csam
        };

    public static bool IsNonOverridable(string category) => NonOverridable.Contains(category);

    public static bool IsReportable(string category) => Reportable.Contains(category);
}

/// <summary>Which layer produced a decision (Spec 096 §6).</summary>
public static class SafetyLayers
{
    /// <summary>Structural constraint — what could be asked at all.</summary>
    public const string Structural = "L1";

    /// <summary>Input classification, before dispatch.</summary>
    public const string Input = "L2";

    /// <summary>Provider safety settings. Necessary, and nowhere near sufficient.</summary>
    public const string Provider = "L3";

    /// <summary>Output classification. The last line before a child.</summary>
    public const string Output = "L4";

    /// <summary>Guardian review. Optional and secondary — a parent is not a moderation queue.</summary>
    public const string Guardian = "L5";
}

public static class SafetyModalities
{
    public const string Text = "text";
    public const string Image = "image";
    public const string Video = "video";
    public const string Speech = "speech";

    /// <summary>
    /// Modalities that unfold over time, where "was it classified?" has a second half: <em>how much
    /// of it</em> (Spec 096 §17 S6, F6).
    ///
    /// <para>
    /// A still image is either classified or it is not. A video is not: a harmful frame <em>between</em>
    /// sample points is delivered even when every sampled frame passes, so "we classified it" can be
    /// true and mean almost nothing. Speech has the same hole for the same reason, which is why it is
    /// here too rather than only video.
    /// </para>
    /// </summary>
    public static readonly IReadOnlySet<string> Temporal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Video, Speech };

    public static bool IsTemporal(string modality) => Temporal.Contains(modality);
}

/// <summary>
/// How much of a temporal artefact a classifier actually covered (Spec 096 F6).
///
/// <para>
/// This exists so that <strong>"frame sampling alone never satisfies this criterion"</strong> is a
/// property of the code rather than a sentence in a document. A classifier declares its coverage and
/// the gate refuses anything short of complete — so an implementer who later ships sampling gets a
/// refusal, not a silent downgrade of the guarantee.
/// </para>
/// </summary>
public interface ITemporalCoverage
{
    TemporalCoverage Coverage { get; }
}

public enum TemporalCoverage
{
    /// <summary>
    /// Nothing was declared. <strong>The zero value deliberately is not <see cref="Complete"/></strong>:
    /// an uninitialised auto-property, a missing configuration value or a default deserialisation would
    /// otherwise silently claim full coverage, which is the one claim that must never be made by
    /// accident. Treated exactly as <see cref="Sampled"/>.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Only sample points were classified. <strong>Never acceptable for child-facing delivery</strong>:
    /// sampling cannot establish that a video is safe, and it cannot coexist with L4 being the last
    /// line before a child. "We sampled every second frame" is not a sentence anyone wants to say to a
    /// parent.
    /// </summary>
    Sampled = 1,

    /// <summary>Every frame, or every sample, of the delivered artefact was classified.</summary>
    Complete = 2,
}

/// <summary>
/// One classifier, expressed as a <strong>routable use case</strong> rather than a vendor.
///
/// <para>
/// Selecting an implementation by vendor and local configuration would build a second
/// provider-routing mechanism alongside the platform rule that all model calls resolve through
/// <c>AiRoutePolicy</c>. That is not merely a compliance point: the gate fails closed, so a
/// classifier vendor outage takes the product down — and central routing is exactly where a second
/// provider is configured, which is how redundancy is actually delivered rather than asserted.
/// </para>
/// </summary>
public interface IContentClassifier
{
    /// <summary>The modality this classifies. One of <see cref="SafetyModalities"/>.</summary>
    string Modality { get; }

    /// <summary>
    /// Classify, returning the categories that fired with their confidence. Throwing is a legitimate
    /// outcome and the gate treats it as <see cref="SafetyDecisionOutcome.CheckUnavailable"/> —
    /// never as "safe".
    /// </summary>
    Task<ClassificationResult> ClassifyAsync(
        ClassificationRequest request,
        CancellationToken cancellationToken = default);
}

/// <param name="SubjectPartyId">
/// Carried because routing must be intersected with <em>this subject's</em> consented provider list
/// (&sect;16.1) — a classifier cannot check that without knowing whose content it is holding.
/// </param>
/// <param name="Reference">Where the content is. Never the content itself.</param>
public sealed record ClassificationRequest(
    Guid SubjectPartyId,
    string SafetyBand,
    string Reference);

/// <param name="Scores">Category to confidence, 0–1. Only categories that fired need appear.</param>
/// <param name="RunId">
/// The <c>AiRun</c> this classification produced. Required: AONIK's fourth non-negotiable is that
/// every AI action is auditable, and a safety classifier acting on behalf of a child is the last
/// place to make an exception.
/// </param>
/// <param name="AdditionalRunIds">
/// Further runs behind the same verdict. <strong>Speech needs this</strong>: judging narration means
/// transcribing it, classifying the transcript, and separately classifying the audio — three runs, one
/// verdict. Recording only the first would leave the decision half-reconstructible, which is the
/// specific failure §15's run ids exist to prevent.
/// </param>
public sealed record ClassificationResult(
    IReadOnlyDictionary<string, double> Scores,
    Guid RunId,
    IReadOnlyList<Guid>? AdditionalRunIds = null)
{
    /// <summary>Every run behind this verdict, in the order they ran.</summary>
    public IReadOnlyList<Guid> AllRunIds => [RunId, .. AdditionalRunIds ?? []];
}

/// <summary>
/// Resolves band plus category to a threshold and an action (Spec 096 §15). Policy is <em>data</em>,
/// so tuning does not require a deployment — and versioned, because a threshold change must be
/// attributable when reviewing an old verdict.
/// </summary>
public interface ISafetyPolicyReader
{
    Task<SafetyPolicySnapshot> GetAsync(string safetyBand, CancellationToken cancellationToken = default);
}

/// <param name="Version">
/// Persisted on every decision. Without it, a reviewer looking at last month's block must infer the
/// applicable threshold from timestamps — guesswork the moment a policy is changed and changed back.
/// </param>
/// <param name="Thresholds">Category to the confidence at or above which content is blocked.</param>
public sealed record SafetyPolicySnapshot(
    string Version,
    string SafetyBand,
    IReadOnlyDictionary<string, double> Thresholds)
{
    /// <summary>
    /// Unknown categories block at a low threshold rather than passing. A classifier that grows a
    /// new label must not become silently unenforced.
    /// </summary>
    public const double UnknownCategoryThreshold = 0.5;

    public double ThresholdFor(string category)
        => Thresholds.TryGetValue(category, out var threshold) ? threshold : UnknownCategoryThreshold;
}
