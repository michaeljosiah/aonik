using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
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

        // An unknown band is treated as the strictest, not as an adult. Same reasoning as Spec 095's
        // unmapped jurisdiction: the wrong-way default is the one that ends badly, and being
        // over-strict costs a support conversation rather than an incident.
        var band = string.IsNullOrWhiteSpace(request.SafetyBand)
            ? SafetyBandDefaults.Strictest
            : request.SafetyBand;

        var policy = await _policyReader.GetAsync(band, cancellationToken);
        var classifier = _classifiers.FirstOrDefault(c =>
            string.Equals(c.Modality, modality, StringComparison.OrdinalIgnoreCase));

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

            return await RefuseAsync(
                request, band, modality, layer, policy, SafetyDecisionOutcome.CheckUnavailable,
                categories: [], classifierRunIds: [], now, cancellationToken);
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
                fired, [result.RunId], now, cancellationToken);
        }

        var decisionId = await _recorder.RecordAsync(
            new SafetyDecisionRecord(
                tenantId, request.SubjectPartyId, band, modality, layer,
                SafetyDecisionOutcome.Allowed, Categories: [], policy.Version,
                request.GenerationRunId, [result.RunId], now),
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
            issuePermit ? new ContentDeliveryPermit(decisionId, request.SubjectPartyId, band) : null);
    }

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

        // Never a permit. The absence is the enforcement: a caller cannot deliver by ignoring this
        // result, because it has nothing to deliver with.
        return new SafetyVerdict(Allowed: false, outcome, categories, decisionId, Permit: null);
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
