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
                fired, result.AllRunIds, now, cancellationToken);
        }

        var decisionId = await _recorder.RecordAsync(
            new SafetyDecisionRecord(
                tenantId, request.SubjectPartyId, band, modality, layer,
                SafetyDecisionOutcome.Allowed, Categories: [], policy.Version,
                request.GenerationRunId, result.AllRunIds, now),
            cancellationToken);

        // Guardian pre-review (§8) sits HERE — after every automated layer has allowed the content,
        // never before. Approving a held item can therefore only release something already judged
        // safe: a guardian cannot click past the gate, whatever the product UI later offers them.
        if (issuePermit
            && await _preReview.RequiresPreReviewAsync(request.SubjectPartyId, band, cancellationToken))
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
        CancellationToken cancellationToken)
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
            await _recorder.RecordIncidentAsync(
                decisionId, request.SubjectPartyId, MostSevere(categories), now, cancellationToken);
        }

        await ReleaseReservationAsync(request, outcome, cancellationToken);

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
