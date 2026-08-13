using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Aonik.SharedKernel.Abstractions.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Spec 096 §16 — the safety boundary, and the only thing that issues a
/// <see cref="ContentDeliveryPermit"/>.
///
/// <para>
/// Everything here is shaped by one asymmetry: the failure is not statistical. A 0.1% error rate on
/// invoice categorisation is a good system; <strong>one frightening image reaching one seven-year-old
/// is a complete failure</strong> — for that child, for their parent, and for a product that will
/// afterwards be described by what it did that once. So the gate fails closed, records every
/// attempt, and never treats an unavailable check as a pass.
/// </para>
/// </summary>
internal sealed class ContentSafetyGate : IContentSafetyGate
{
    private readonly AiDbContext _dbContext;
    private readonly ISafetyPolicyReader _policyReader;
    private readonly IEnumerable<IContentClassifier> _classifiers;
    private readonly ISafetyIncidentRecorder _recorder;
    private readonly IGuardianPreReviewService _preReview;
    private readonly ISafetyBandReader _bandReader;
    private readonly IPreservedInputStore? _preservedInputStore;
    private readonly IUsageMeter? _usageMeter;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly IOptions<SafetyOptions> _options;
    private readonly ILogger<ContentSafetyGate> _logger;

    public ContentSafetyGate(
        AiDbContext dbContext,
        ISafetyPolicyReader policyReader,
        IEnumerable<IContentClassifier> classifiers,
        ISafetyIncidentRecorder recorder,
        IGuardianPreReviewService preReview,
        ISafetyBandReader bandReader,
        IPreservedInputStore? preservedInputStore,
        IUsageMeter? usageMeter,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<SafetyOptions> options,
        ILogger<ContentSafetyGate> logger)
    {
        _dbContext = dbContext;
        _policyReader = policyReader;
        _classifiers = classifiers;
        _recorder = recorder;
        _preReview = preReview;
        _bandReader = bandReader;
        _preservedInputStore = preservedInputStore;
        _usageMeter = usageMeter;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    public Task<SafetyVerdict> ScreenInputAsync(
        SafetyRequest request, string input, CancellationToken cancellationToken = default)
        // No generation run exists yet — L2 runs before dispatch, which is the whole point of it
        // being the cheap layer. The decision record's generation reference stays null.
        => EvaluateAsync(
            request with { GenerationRunId = null },
            SafetyLayers.Input,
            SafetyModalities.Text,
            input,
            issuePermit: false,
            cancellationToken);

    public Task<SafetyVerdict> ScreenOutputAsync(
        SafetyRequest request, GeneratedContent content, CancellationToken cancellationToken = default)
        => EvaluateAsync(
            request,
            SafetyLayers.Output,
            content.Modality,
            content.Reference,
            issuePermit: true,
            cancellationToken);

    private async Task<SafetyVerdict> EvaluateAsync(
        SafetyRequest request,
        string layer,
        string modality,
        string reference,
        bool issuePermit,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        // Read from the party record, never from the request. A caller-supplied band could claim
        // `adult` for a six-year-old and skip every threshold and every guardian hold with one field.
        //
        // An unresolvable band is treated as the strictest, not as an adult. Same reasoning as
        // Spec 095's unmapped jurisdiction: the wrong-way default is the one that ends badly, and
        // being over-strict costs a support conversation rather than an incident.
        var band = await ResolveBandAsync(request.SubjectPartyId, cancellationToken);

        var policy = await _policyReader.GetAsync(band, cancellationToken);

        if (!IsModalityEnabled(modality))
        {
            // A switched-off modality is a policy state, not an outage. Video is off because F6 is a
            // product decision nobody has taken, and paging an operator every time something asks for
            // it would train them to ignore the alert that matters.
            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.ModalityDisabled,
                categories: [], classifierRunIds: [], now, cancellationToken);
        }

        var classifier = _classifiers.FirstOrDefault(c =>
            string.Equals(c.Modality, modality, StringComparison.OrdinalIgnoreCase));

        if (classifier is not null
            && SafetyModalities.IsTemporal(modality)
            && !HasCompleteTemporalCoverage(classifier))
        {
            // The S6 acceptance criterion, executable: "frame sampling alone never satisfies this."
            // A sampling classifier passes every frame it looks at and delivers the one between
            // sample points, so the refusal cannot depend on the classifier noticing anything — it
            // depends on what the classifier CLAIMS to have covered. Pages, because someone shipped
            // a design this spec rejects and it needs fixing rather than tolerating.
            _logger.LogError(
                "Classifier for temporal modality {Modality} does not declare complete coverage. "
                + "Refusing delivery: sampling cannot establish that a generation is safe.", modality);

            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.CheckUnavailable,
                categories: [], classifierRunIds: [], now, cancellationToken);
        }

        if (classifier is null)
        {
            // No classifier for this modality means we cannot judge it, and unjudged content is not
            // delivered. A missing classifier is an unavailable feature, not an unchecked one.
            _logger.LogError(
                "No content classifier registered for modality {Modality}; refusing delivery.", modality);

            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.CheckUnavailable,
                categories: [], classifierRunIds: [], now, cancellationToken);
        }

        ClassificationResult result;

        try
        {
            result = await classifier.ClassifyAsync(
                new ClassificationRequest(request.SubjectPartyId, band, reference), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Fails closed. A silently degraded classifier that returns "safe" on error is the worst
            // possible defect here, and this catch is the specific thing that prevents it.
            _logger.LogError(ex,
                "Content classification failed for {Modality}; refusing delivery and paging.", modality);

            // A multi-leg classifier may have completed runs before failing. Recording the outage
            // without them would leave an audit trail disconnected from AI executions that actually
            // happened — the gap §15's run ids exist to close.
            var completedRunIds = ex is SpeechClassificationFailedException partial
                ? partial.CompletedRunIds
                : [];

            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.CheckUnavailable,
                categories: [], completedRunIds, now, cancellationToken);
        }

        var fired = result.Scores
            .Where(score => score.Value >= policy.ThresholdFor(score.Key))
            .Select(score => score.Key)
            .OrderBy(category => category, StringComparer.Ordinal)
            .ToList();

        if (fired.Count > 0)
        {
            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.Blocked,
                fired, result.AllRunIds, now, cancellationToken,
                // An OUTPUT reference is already a durable storage key, so it goes straight on.
                contentReference: issuePermit ? reference : null,
                // An INPUT is the child's own words. It is preserved only at the reportable category,
                // and only AFTER the incident exists — see PreserveAfterRecordingAsync.
                inputToPreserve: !issuePermit && fired.Any(SafetyCategories.IsReportable)
                    ? reference
                    : null);
        }

        // Guardian pre-review (§8) sits HERE — after every automated layer has allowed the content,
        // never before. Approving a held item can therefore only release something already judged
        // safe: a guardian cannot click past the gate, whatever the product UI later offers them.
        bool held;

        try
        {
            held = issuePermit
                && await _preReview.RequiresPreReviewAsync(request.SubjectPartyId, band, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Resolving pre-review happens BEFORE the decision is recorded, so a failure here would
            // otherwise leave a completed classifier run with no decision linking it to the attempted
            // delivery — and no reservation released. Fails closed like any other unavailable check:
            // holding instead would need the same database that just refused us.
            _logger.LogError(ex,
                "Could not resolve guardian pre-review for {SubjectId}; refusing delivery.",
                request.SubjectPartyId);

            // Released FIRST and independently. RefuseAsync records through the same scoped
            // DbContext that just refused us, so during a real outage that write throws too — and a
            // release sequenced after it would never happen, which is the exact failure this handler
            // exists to prevent.
            await ReleaseReservationAsync(request, SafetyDecisionOutcome.CheckUnavailable, cancellationToken);

            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.CheckUnavailable,
                categories: [], result.AllRunIds, now, cancellationToken,
                releaseReservation: false);
        }

        // Recorded as HeldForReview from the outset rather than Allowed-then-held. The hold row is
        // deleted once it expires, so a decision saying "allowed" would leave an audit reconstructing
        // this as delivered — with nothing left to show it never reached the child.
        var decisionId = await _recorder.RecordAsync(
            new SafetyDecisionRecord(
                tenantId, request.SubjectPartyId, band, modality, layer,
                held ? SafetyDecisionOutcome.HeldForReview : SafetyDecisionOutcome.Allowed,
                Categories: [], policy.Version,
                request.GenerationRunId, result.AllRunIds, now),
            cancellationToken);

        if (held)
        {
            return await HoldAsync(request, band, modality, reference, decisionId, now, cancellationToken);
        }

        // Every attempt is recorded, allowed included. A delivery later identified as a false
        // negative must be reconstructible — which is exactly what a blocks-only log cannot do.
        return new SafetyVerdict(
            Allowed: true,
            SafetyDecisionOutcome.Allowed,
            Categories: [],
            decisionId,
            issuePermit
                ? new ContentDeliveryPermit(
                    decisionId, request.SubjectPartyId, band, modality, reference)
                : null);
    }

    /// <summary>
    /// Preserves a reportable <em>input</em>, after the incident that will point at it exists.
    ///
    /// <para>
    /// Ordering is the whole point. An external store write that happened first would, on a failed
    /// database write, take the only copy of the key with it — leaving reportable child content in
    /// protected storage with no legal-hold row, no access audit, no escalation and no cleanup path.
    /// Writing the incident first cannot make the external call transactional, but it narrows the
    /// window to an incident that names its own failure, and the key is logged at critical so it stays
    /// recoverable by hand. A durable outbox would close it properly; this is honest about not being one.
    /// </para>
    ///
    /// <para>
    /// Ordinary input blocks never reach here. §11's position is that a child's own input is not
    /// material we keep, and only the reportable category inverts it.
    /// </para>
    /// </summary>
    private async Task PreserveAfterRecordingAsync(
        SafetyRequest request,
        Guid incidentId,
        string input,
        DateTime now,
        CancellationToken cancellationToken)
    {
        if (_preservedInputStore is null)
        {
            _logger.LogCritical(
                "A reportable category fired on INPUT for subject {SubjectId} (incident {IncidentId}) "
                + "and no preserved-input store is configured. §12 requires preservation and none "
                + "happened — the escalation records this, and it needs acting on now.",
                request.SubjectPartyId, incidentId);

            await MarkPreservationFailedAsync(incidentId, cancellationToken);
            return;
        }

        string reference;

        try
        {
            reference = await _preservedInputStore.PreserveAsync(
                request.SubjectPartyId, input, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never swallowed into a success. A preservation failure recorded as preserved is the
            // shape of mistake §12 exists to prevent.
            _logger.LogCritical(ex,
                "Preserving a reportable input for incident {IncidentId} failed. The block still "
                + "stands; the material is gone.", incidentId);

            await MarkPreservationFailedAsync(incidentId, cancellationToken);
            return;
        }

        try
        {
            await _recorder.AttachPreservedMaterialAsync(incidentId, reference, now, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The reference is deliberately NOT logged. PreservedMaterialService releases it only
            // after the named-custodian check and records every attempt; emitting it here would put
            // the key in the ordinary logging pipeline, where anyone with log access could obtain it
            // outside both controls — a worse outcome than the orphan itself.
            //
            // Recovery is therefore by reconciling the store against SafetyArtefacts (objects with no
            // matching row), not by reading a key out of a log.
            _logger.LogCritical(ex,
                "Preserved reportable material for incident {IncidentId} but could not link it. The "
                + "object exists in the protected store and is NOT under a recorded hold — reconcile "
                + "the store against SafetyArtefacts to find it.", incidentId);
        }
    }

    /// <summary>Best-effort: a failure to record the failure must not mask the original one.</summary>
    private async Task MarkPreservationFailedAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        try
        {
            await _recorder.MarkPreservationFailedAsync(incidentId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex,
                "Could not record that preservation failed for incident {IncidentId}.", incidentId);
        }
    }

    private async Task<string> ResolveBandAsync(Guid subjectPartyId, CancellationToken cancellationToken)
    {
        string? band = null;

        try
        {
            band = await _bandReader.GetSafetyBandAsync(subjectPartyId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A band we cannot read is a band we do not know, and the answer to not knowing is
            // always the strictest one. Rethrowing would turn a Platform hiccup into an outage for a
            // path that has a perfectly safe fallback.
            _logger.LogError(ex,
                "Could not resolve the safety band for {SubjectId}; applying the strictest band.",
                subjectPartyId);
        }

        return string.IsNullOrWhiteSpace(band) ? SafetyBandDefaults.Strictest : band;
    }

    private bool IsModalityEnabled(string modality)
        => _options.Value.ResolvedModalities
            .Contains(modality, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// A temporal classifier must <em>declare</em> complete coverage, and one that says nothing is
    /// treated as not having it.
    ///
    /// <para>
    /// Silence reads as sampling rather than as completeness, which is the wrong-way default applied
    /// once more: a classifier that has not thought about coverage has almost certainly not achieved
    /// it, and the cost of being wrong here is a harmful frame reaching a child.
    /// </para>
    /// </summary>
    private static bool HasCompleteTemporalCoverage(IContentClassifier classifier)
        => classifier is ITemporalCoverage declared
            && declared.Coverage == TemporalCoverage.Complete;

    private async Task<SafetyVerdict> HoldAsync(
        SafetyRequest request,
        string band,
        string modality,
        string reference,
        Guid decisionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        _dbContext.PendingContentReviews.Add(new PendingContentReview
        {
            TenantId = _tenantProvider.GetCurrentTenantId(),
            SafetyDecisionId = decisionId,
            SubjectPartyId = request.SubjectPartyId,
            SafetyBand = band,
            Modality = modality,
            Reference = reference,
            State = PreReviewStates.Pending,
            HeldAt = now,
            ExpiresAt = now.AddDays(_options.Value.PreReviewHoldDays),
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
        await ReleaseReservationAsync(request, SafetyDecisionOutcome.HeldForReview, cancellationToken);

        // No permit, and no incident: nothing was judged unsafe. HeldForReview is deliberately not a
        // refusal — it must not page like an outage, and the guardian must not be alarmed by it.
        return new SafetyVerdict(
            Allowed: false, SafetyDecisionOutcome.HeldForReview, Categories: [], decisionId, Permit: null);
    }

    private async Task<SafetyVerdict> RefuseAsync(
        SafetyRequest request,
        string band,
        string modality,
        string layer,
        SafetyPolicySnapshot policy,
        SafetyDecisionOutcome outcome,
        IReadOnlyList<string> categories,
        IReadOnlyList<Guid> classifierRunIds,
        DateTime now,
        CancellationToken cancellationToken,
        string? contentReference = null,
        string? inputToPreserve = null,
        bool releaseReservation = true)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var decisionId = await _recorder.RecordAsync(
            new SafetyDecisionRecord(
                tenantId, request.SubjectPartyId, band, modality, layer,
                outcome, categories, policy.Version,
                request.GenerationRunId, classifierRunIds, now),
            cancellationToken);

        if (outcome == SafetyDecisionOutcome.Blocked && categories.Count > 0)
        {
            // The reference travels with the incident so an artefact is written. Passed only on a
            // Blocked outcome: an unavailable check judged nothing, and preserving content we never
            // classified would be retention without a reason.
            var incidentId = await _recorder.RecordIncidentAsync(
                decisionId, request.SubjectPartyId, MostSevere(categories),
                contentReference ?? string.Empty, now, cancellationToken);

            if (inputToPreserve is not null)
            {
                await PreserveAfterRecordingAsync(
                    request, incidentId, inputToPreserve, now, cancellationToken);
            }
        }

        if (releaseReservation)
        {
            await ReleaseReservationAsync(request, outcome, cancellationToken);
        }

        // Never a permit. The absence is the enforcement: a caller cannot deliver by ignoring this
        // result, because it has nothing to deliver with.
        return new SafetyVerdict(Allowed: false, outcome, categories, decisionId, Permit: null);
    }

    /// <summary>
    /// Nothing the child was not shown is billed (§10.1, §10.2, §18.6).
    ///
    /// <para>
    /// Released on <em>every</em> non-allowed outcome, including <c>HeldForReview</c>. A hold can last
    /// two weeks and a meter reservation cannot, so keeping it would only mean the platform's own
    /// sweeper expiring it later; and charging a family for a story their parent had to approve by
    /// hand is a worse trade than the credit is worth.
    /// </para>
    ///
    /// <para>
    /// Wrapped, because a billing failure must never become a delivery. If the release throws, the
    /// content still does not go out — the family is over-charged by one credit, which is a support
    /// conversation rather than an incident.
    /// </para>
    /// </summary>
    private async Task ReleaseReservationAsync(
        SafetyRequest request, SafetyDecisionOutcome outcome, CancellationToken cancellationToken)
    {
        if (request.UsageReservationId is not { } reservationId
            || outcome == SafetyDecisionOutcome.Allowed
            || _usageMeter is null)
        {
            return;
        }

        try
        {
            await _usageMeter.ReleaseAsync(reservationId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Failed to release usage reservation {ReservationId} after a {Outcome} verdict. "
                + "The content is still withheld; the family may have been charged for it.",
                reservationId, outcome);
        }
    }

    /// <summary>
    /// The category an incident is filed under when several fire. Ordered by consequence, not by
    /// score: a reportable category must win even at lower confidence, because the response it
    /// triggers is not a moderation decision.
    /// </summary>
    private static string MostSevere(IReadOnlyList<string> categories)
    {
        foreach (var category in SafetySeverityOrder.Descending)
        {
            if (categories.Contains(category, StringComparer.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return categories[0];
    }
}

internal static class SafetySeverityOrder
{
    public static readonly IReadOnlyList<string> Descending =
    [
        SafetyCategories.Csam,
        SafetyCategories.Sexual,
        SafetyCategories.SelfHarm,
        SafetyCategories.GraphicViolence,
        SafetyCategories.Hate,
        SafetyCategories.RealPersonLikeness,
        SafetyCategories.Frightening,
    ];
}

internal static class SafetyBandDefaults
{
    /// <summary>
    /// Applied when the band is unknown. Deliberately the strictest — a party whose age we cannot
    /// establish is treated as the youngest, not as an adult.
    /// </summary>
    public const string Strictest = "under-6";
}
