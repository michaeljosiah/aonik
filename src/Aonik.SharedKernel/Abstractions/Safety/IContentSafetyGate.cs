namespace Aonik.SharedKernel.Abstractions.Safety;

/// <summary>
/// The safety boundary for child-facing generation (Spec 096 §16). Fails closed.
///
/// <para>
/// <strong>Where this sits, and why it does not break the ADR-016 seam.</strong> Safety is a property
/// of <em>generation</em>, not of <em>storage</em>: it runs while content is still a request and a
/// response, which is the only moment the prompt, the model, the age band and the output are all in
/// hand at once. By the time bytes reach <c>Aonik.Workspaces</c> they have already been judged, so
/// the storage layer's ignorance of file contents is preserved intact.
/// </para>
///
/// <para>
/// The corollary is a real limit rather than an oversight: a file arriving by <em>import</em> or
/// <em>sync</em> is not inspected, by anyone. We are responsible for what we generate, and a product
/// that quietly began scanning everything a family stored would be a different and much worse thing.
/// </para>
/// </summary>
public interface IContentSafetyGate
{
    /// <summary>
    /// Classify a child's input before anything is dispatched (L2).
    ///
    /// <para>
    /// Cheap and first, deliberately: it catches the deliberate cases before any money is spent, and
    /// a block here means no generation run exists at all — which is why the decision record's
    /// generation reference is nullable.
    /// </para>
    /// </summary>
    Task<SafetyVerdict> ScreenInputAsync(
        SafetyRequest request,
        string input,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Classify generated output before a child sees it (L4) — the last line before delivery.
    ///
    /// <para>
    /// A <see cref="SafetyVerdict.Permit"/> is the only thing that authorises delivery, and only this
    /// method issues one. That is the enforcement: a caller cannot deliver by forgetting to check,
    /// because it has nothing to deliver <em>with</em>.
    /// </para>
    /// </summary>
    Task<SafetyVerdict> ScreenOutputAsync(
        SafetyRequest request,
        GeneratedContent content,
        CancellationToken cancellationToken = default);
}

/// <param name="SubjectPartyId">
/// The child the content is for. Their band drives every threshold, and the gate resolves it through
/// <see cref="ISafetyBandReader"/> — <strong>the caller does not supply it</strong>. A request that
/// carried its own band could claim <c>adult</c> for a six-year-old and skip every threshold and every
/// guardian hold with one field.
/// </param>
/// <param name="Modality">Text, image, video or speech.</param>
/// <param name="GenerationRunId">
/// The <c>AiRun</c> of the generation, when one exists. <strong>Null for an input-stage screen</strong>,
/// because L2 runs before dispatch — requiring it would force an implementation to fabricate an AI
/// execution that never happened in order to log a block correctly.
/// </param>
/// <param name="UsageReservationId">
/// The Spec 087 meter hold covering this generation, when the caller took one. <strong>The gate
/// releases it on any outcome other than <see cref="SafetyDecisionOutcome.Allowed"/></strong> — §10.1
/// is blunt that charging a family a story credit for content we refused to show is indefensible, and
/// leaving that to caller discipline means it is wrong on the paths nobody tested.
/// </param>
public sealed record SafetyRequest(
    Guid SubjectPartyId,
    string Modality,
    Guid? GenerationRunId = null,
    Guid? UsageReservationId = null);

/// <param name="Reference">Where the content is, for the classifier. Never inlined into a record.</param>
public sealed record GeneratedContent(string Modality, string Reference);

/// <summary>
/// What the gate decided. A verdict is always recorded, whether or not it permitted delivery — a
/// delivery later identified as a false negative must be reconstructible, and an audit trail that
/// only covers blocks covers everything except the failure it is for.
/// </summary>
public sealed record SafetyVerdict(
    bool Allowed,
    SafetyDecisionOutcome Outcome,
    IReadOnlyList<string> Categories,
    Guid DecisionId,
    ContentDeliveryPermit? Permit)
{
    /// <summary>
    /// True when the content was withheld because a check could not be performed, rather than
    /// because it was judged unsafe. Operationally this is an <strong>outage and must page</strong>:
    /// a silently degraded classifier returning "safe" on error is the worst defect available here.
    /// </summary>
    public bool WasUnavailable => Outcome == SafetyDecisionOutcome.CheckUnavailable;

    /// <summary>
    /// True when the modality is switched off. <strong>Not an outage and must not page</strong> — a
    /// feature that was never enabled cannot be "down", and conflating the two means an operator is
    /// paged forever for video that nobody turned on.
    /// </summary>
    public bool WasDisabled => Outcome == SafetyDecisionOutcome.ModalityDisabled;
}

public enum SafetyDecisionOutcome
{
    /// <summary>Passed every layer. The only outcome carrying a permit.</summary>
    Allowed = 0,

    /// <summary>Judged unsafe. The child sees a plain message, never a category name.</summary>
    Blocked = 1,

    /// <summary>A classifier errored, timed out, or was unavailable. Fails closed.</summary>
    CheckUnavailable = 2,

    /// <summary>Held for guardian pre-review before delivery (Spec 096 §8). Not a refusal.</summary>
    HeldForReview = 3,

    /// <summary>
    /// The modality is switched off (Spec 096 S6). Distinct from <see cref="CheckUnavailable"/>: a
    /// disabled feature is a policy state, an unavailable check is an outage, and only one of them
    /// should wake somebody up.
    /// </summary>
    ModalityDisabled = 4,
}

/// <summary>
/// Unforgeable proof that <see cref="IContentSafetyGate"/> allowed this content.
///
/// <para>
/// Its constructor is internal to this assembly and exposed only to the safety module, so
/// <strong>no caller can manufacture one</strong>. Delivery paths take a permit rather than a
/// boolean, which turns "did you remember to check?" from a convention into a compile-time
/// requirement — the same shape as
/// <a href="../../../../docs/specifications/032.tiered-ai-mutation-approval.html">Spec 032</a>'s
/// refusal to let an unclassified tool exist, and the reason Spec 096 §16 asks for a gate that
/// cannot be forgotten rather than one that is merely available.
/// </para>
/// </summary>
public sealed class ContentDeliveryPermit
{
    internal ContentDeliveryPermit(
        Guid decisionId, Guid subjectPartyId, string safetyBand, string modality, string contentReference)
    {
        DecisionId = decisionId;
        SubjectPartyId = subjectPartyId;
        SafetyBand = safetyBand;
        Modality = modality;
        ContentReference = contentReference;
    }

    /// <summary>The recorded decision this permit came from, so delivery is traceable to a verdict.</summary>
    public Guid DecisionId { get; }

    public Guid SubjectPartyId { get; }

    public string SafetyBand { get; }

    /// <summary>The modality that was classified. A speech permit does not authorise an image.</summary>
    public string Modality { get; }

    /// <summary>
    /// The <em>exact</em> content this permit covers.
    ///
    /// <para>
    /// Without it a permit is a bearer token for the subject rather than for the artefact: hold any
    /// valid one and you can pair it with a different reference, laundering unclassified content
    /// through a delivery type that looks checked. <see cref="Authorises"/> is what closes that.
    /// </para>
    /// </summary>
    public string ContentReference { get; }

    /// <summary>Whether this permit covers exactly this artefact in exactly this modality.</summary>
    public bool Authorises(string modality, string contentReference)
        => string.Equals(Modality, modality, StringComparison.OrdinalIgnoreCase)
            && string.Equals(ContentReference, contentReference, StringComparison.Ordinal);
}
