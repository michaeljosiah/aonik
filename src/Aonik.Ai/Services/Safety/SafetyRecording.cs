using System.Text.Json;
using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;
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

    Task RecordIncidentAsync(
        Guid decisionId,
        Guid subjectPartyId,
        string category,
        DateTime occurredAt,
        CancellationToken cancellationToken = default);
}

internal sealed class SafetyIncidentRecorder : ISafetyIncidentRecorder
{
    private readonly AiDbContext _dbContext;
    private readonly SafetyOptions _options;

    public SafetyIncidentRecorder(AiDbContext dbContext, IOptions<SafetyOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
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
        DateTime occurredAt,
        CancellationToken cancellationToken = default)
    {
        var decision = await _dbContext.SafetyDecisions
            .AsNoTracking()
            .FirstAsync(d => d.Id == decisionId, cancellationToken);

        // Denormalised at write time, deliberately. Deriving these at read time would let a later
        // policy edit retroactively make a sealed incident releasable, or drop a legal hold that was
        // correct when it was applied.
        _dbContext.SafetyIncidents.Add(new SafetyIncident
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
        });

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
}
