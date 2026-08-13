using System.Text.Json;
using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Ai.Services.Safety;

/// <param name="ClassifierRunIds">Always at least one. Rule 4: every AI action is auditable.</param>
public sealed record SafetyDecisionRecord(
    Guid TenantId,
    Guid SubjectPartyId,
    string SafetyBand,
    string Modality,
    string Layer,
    SafetyDecisionOutcome Outcome,
    IReadOnlyList<string> Categories,
    string SafetyPolicyVersion,
    Guid? GenerationRunId,
    IReadOnlyList<Guid> ClassifierRunIds,
    DateTime DecidedAt);

/// <summary>
/// Writes the decision and, for a block, its incident (Spec 096 §15).
/// </summary>
public interface ISafetyIncidentRecorder
{
    Task<Guid> RecordAsync(SafetyDecisionRecord record, CancellationToken cancellationToken = default);

    /// <param name="contentReference">
    /// Where the blocked content is. <strong>Required, and the reason this parameter exists at all</strong>:
    /// without it nothing ever writes a <c>SafetyArtefact</c>, so the guardian appeal flow has nothing
    /// to show and the retention sweeper has nothing to sweep. Both would look implemented and do
    /// nothing — the artefact table would only ever be populated by tests.
    /// </param>
    Task RecordIncidentAsync(
        Guid decisionId,
        Guid subjectPartyId,
        string category,
        string contentReference,
        DateTime occurredAt,
        CancellationToken cancellationToken = default);
}

internal sealed class SafetyIncidentRecorder : ISafetyIncidentRecorder
{
    private readonly AiDbContext _dbContext;
    private readonly SafetyOptions _options;
    private readonly ILogger<SafetyIncidentRecorder> _logger;

    public SafetyIncidentRecorder(
        AiDbContext dbContext,
        IOptions<SafetyOptions> options,
        ILogger<SafetyIncidentRecorder> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Guid> RecordAsync(
        SafetyDecisionRecord record, CancellationToken cancellationToken = default)
    {
        var decision = new SafetyDecision
        {
            Id = Guid.NewGuid(),
            TenantId = record.TenantId,
            SubjectPartyId = record.SubjectPartyId,
            SafetyBand = record.SafetyBand,
            Modality = record.Modality,
            Layer = record.Layer,
            Outcome = record.Outcome.ToString(),
            Categories = record.Categories.Count == 0 ? null : string.Join(',', record.Categories),
            SafetyPolicyVersion = record.SafetyPolicyVersion,
            GenerationRunId = record.GenerationRunId,
            ClassifierRunIds = record.ClassifierRunIds.Count == 0
                ? null
                : string.Join(',', record.ClassifierRunIds),
            DecidedAt = record.DecidedAt,
            ExpiresAt = record.DecidedAt.AddDays(_options.DecisionRetentionDays)
        };

        _dbContext.SafetyDecisions.Add(decision);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return decision.Id;
    }

    public async Task RecordIncidentAsync(
        Guid decisionId,
        Guid subjectPartyId,
        string category,
        string contentReference,
        DateTime occurredAt,
        CancellationToken cancellationToken = default)
    {
        var decision = await _dbContext.SafetyDecisions
            .AsNoTracking()
            .FirstAsync(d => d.Id == decisionId, cancellationToken);

        // Denormalised at write time, deliberately. Deriving these at read time would let a later
        // policy edit retroactively make a sealed incident releasable, or drop a legal hold that was
        // correct when it was applied.
        var incident = new SafetyIncident
        {
            Id = Guid.NewGuid(),
            TenantId = decision.TenantId,
            SafetyDecisionId = decisionId,
            SubjectPartyId = subjectPartyId,
            Category = category,
            IsNonOverridable = SafetyCategories.IsNonOverridable(category),
            IsUnderLegalHold = SafetyCategories.IsReportable(category),
            AppealState = SafetyAppealStates.None,
            OccurredAt = occurredAt
        };

        _dbContext.SafetyIncidents.Add(incident);

        // The artefact is what a guardian appeal actually looks at, and what the sweeper deletes.
        // Recording an incident without one leaves both flows implemented and inert: every listing
        // reports CanView = false because there is nothing to view, and retention has nothing to
        // enforce. Kept short by design — storing the very thing we judged unsafe for a child,
        // indefinitely, would be perverse — with the §12 hold as the one exception.
        var preserved = !string.IsNullOrWhiteSpace(contentReference);

        if (preserved)
        {
            _dbContext.SafetyArtefacts.Add(new SafetyArtefact
            {
                Id = Guid.NewGuid(),
                TenantId = decision.TenantId,
                SafetyIncidentId = incident.Id,
                Reference = contentReference,
                ExpiresAt = occurredAt.AddDays(_options.ArtefactRetentionDays),
                IsUnderLegalHold = incident.IsUnderLegalHold
            });
        }

        if (SafetyCategories.IsReportable(category))
        {
            // §12: detection at this category escalates to a person, immediately — so the escalation
            // is written in the SAME call and the same transaction as the incident. An escalation
            // that depends on a scheduler having run is not immediate, and a notification that failed
            // to send leaves no trace at all. A row does, and "nobody acknowledged it" stays
            // queryable, which is the failure that actually happens.
            _dbContext.SafetyEscalations.Add(new SafetyEscalation
            {
                Id = Guid.NewGuid(),
                TenantId = decision.TenantId,
                SafetyIncidentId = incident.Id,
                SubjectPartyId = subjectPartyId,
                Category = category,
                RaisedAt = occurredAt,
                MaterialPreserved = preserved
            });

            // Said out loud either way. An escalation that implies preservation when none happened is
            // a false assurance at the worst possible moment, and the responsible person needs to know
            // whether they are acting on evidence or only on a verdict.
            if (preserved)
            {
                _logger.LogCritical(
                    "Reportable safety category {Category} detected for subject {SubjectId} "
                    + "(incident {IncidentId}). Material is preserved under a §12 hold and escalated to "
                    + "the named responsible person. This is not a moderation decision.",
                    category, subjectPartyId, incident.Id);
            }
            else
            {
                _logger.LogCritical(
                    "Reportable safety category {Category} detected for subject {SubjectId} "
                    + "(incident {IncidentId}) and NO MATERIAL WAS PRESERVED. The verdict survives; the "
                    + "content does not. §12 requires preservation — this needs acting on now.",
                    category, subjectPartyId, incident.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// Reads thresholds per band (Spec 096 §15). Falls back to a built-in strict policy when a tenant
/// has configured none — an unconfigured tenant must be safe, not unguarded.
/// </summary>
internal sealed class SafetyPolicyReader : ISafetyPolicyReader
{
    private const string BuiltInVersion = "builtin-1";

    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;

    public SafetyPolicyReader(AiDbContext dbContext, ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
    }

    public async Task<SafetyPolicySnapshot> GetAsync(
        string safetyBand, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var policy = await _dbContext.SafetyPolicies
            .AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.SafetyBand == safetyBand && p.IsActive)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is null)
        {
            return new SafetyPolicySnapshot(BuiltInVersion, safetyBand, BuiltInThresholds);
        }

        var thresholds = JsonSerializer.Deserialize<Dictionary<string, double>>(policy.ThresholdsJson)
            ?? new Dictionary<string, double>();

        return new SafetyPolicySnapshot(policy.Version, safetyBand, thresholds);
    }

    /// <summary>
    /// Deliberately strict, and deliberately not tuned. S0 agrees the real numbers with product;
    /// until then an unconfigured tenant blocks readily rather than passing readily, because the
    /// cost of being over-strict is a support conversation and the cost of the alternative is not.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, double> BuiltInThresholds =
        new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            [SafetyCategories.Csam] = 0.01,
            [SafetyCategories.Sexual] = 0.05,
            [SafetyCategories.SelfHarm] = 0.10,
            [SafetyCategories.GraphicViolence] = 0.30,
            [SafetyCategories.Hate] = 0.20,
            [SafetyCategories.RealPersonLikeness] = 0.30,
            [SafetyCategories.Frightening] = 0.40,
        };
}

public sealed class SafetyOptions
{
    public const string SectionName = "ContentSafety";

    /// <summary>
    /// How long a decision row is kept in full before anonymisation. A row per generation is a lot
    /// of rows about one child, and §13 argues for minimisation in the same breath as it argues for
    /// being able to answer a parent — so the verdict outlives the subject link.
    /// </summary>
    public int DecisionRetentionDays { get; set; } = 90;

    /// <summary>
    /// How long blocked content is retained for guardian appeal. Short: storing the very thing we
    /// judged unsafe for a child, indefinitely, would be perverse.
    /// </summary>
    public int ArtefactRetentionDays { get; set; } = 7;

    /// <summary>Incidents outlive both — they answer the parent's question and tune the thresholds.</summary>
    public int IncidentRetentionDays { get; set; } = 400;

    /// <summary>
    /// How long content held for guardian pre-review waits before it expires <em>undelivered</em>
    /// (§8). Long enough that a parent who checks weekly does not lose their child's stories, and
    /// finite because a hold nobody acts on must resolve — as expiry, never as approval.
    /// </summary>
    public int PreReviewHoldDays { get; set; } = 14;

    /// <summary>
    /// Modalities a child may be delivered at all (Spec 096 S6).
    ///
    /// <para>
    /// <strong>Video is deliberately absent.</strong> F6 is a product decision that has not been
    /// taken, and the spec is explicit that video staying off is a legitimate outcome rather than a
    /// failure — sampling cannot establish that a video is safe, and it is the only affordable option
    /// on the table. Listing the enabled set makes "off" a configured, testable state rather than an
    /// accident of nobody having registered a classifier.
    /// </para>
    ///
    /// <para>
    /// Adding <c>video</c> here does not enable it on its own: the gate still requires a classifier
    /// declaring <see cref="Aonik.SharedKernel.Abstractions.Safety.TemporalCoverage.Complete"/>. Two
    /// locks, because this is the one a deadline argues hardest against.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Deliberately null-by-default rather than pre-populated. The configuration binder <em>adds</em>
    /// to an already-initialised collection instead of replacing it, so a pre-populated default would
    /// make this allowlist unable to switch anything off: an operator narrowing it to text and image
    /// during a speech incident would still have speech enabled. Null means "nobody configured this",
    /// and <see cref="ResolvedModalities"/> supplies the defaults on read.
    /// </remarks>
    public IList<string>? EnabledModalities { get; set; }

    /// <summary>
    /// The modalities actually in force. <strong>Video is absent from the defaults</strong> — F6 is a
    /// product decision that has not been taken, and the spec is explicit that video staying off is a
    /// legitimate outcome rather than a failure. An explicitly configured empty list means exactly
    /// that: everything off.
    /// </summary>
    public IReadOnlyCollection<string> ResolvedModalities
        => EnabledModalities is { } configured
            ? [.. configured]
            :
        [
            SharedKernel.Abstractions.Safety.SafetyModalities.Text,
            SharedKernel.Abstractions.Safety.SafetyModalities.Image,
            SharedKernel.Abstractions.Safety.SafetyModalities.Speech,
        ];

    /// <summary>
    /// Party ids of the <strong>named individuals</strong> who may reach preserved §12 material.
    ///
    /// <para>
    /// Individuals, not a role: a role grants access to whoever later acquires it, which is exactly
    /// the property §12 rules out. <strong>Empty by default</strong> — F7 has not been resolved, so
    /// nobody can reach it. That is the safe state rather than a lockout to work around, and it is the
    /// state a deployment should be embarrassed to leave in place before launch.
    /// </para>
    /// </summary>
    public IList<string> PreservedMaterialCustodians { get; set; } = [];
}
