using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Enforces the retention rules in Spec 096 §13.
///
/// <para>
/// <strong>This ships with the gate, not after it.</strong> An expiry column deletes nothing, and a
/// retention policy nothing enforces is a statement of intent — invisible precisely because the
/// column looks like the mechanism. Artefacts start accumulating the moment blocking works, so the
/// sweeper cannot be a later phase.
/// </para>
///
/// <para>
/// It resolves the tension §13 names: a parent asking <em>"what did your product show my child?"</em>
/// deserves a real answer, which needs records; the Children's Code requires minimisation, and every
/// retained generation is a record of a child's imagination. So the <em>verdict</em> outlives the
/// <em>artefact</em>, and the subject link outlives neither.
/// </para>
/// </summary>
public interface ISafetyRetentionSweeper
{
    Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(CancellationToken cancellationToken = default);

    Task<SafetySweepSummary> SweepAsync(CancellationToken cancellationToken = default);
}

public sealed record SafetySweepSummary(
    int ArtefactsDeleted, int ArtefactsHeld, int DecisionsAnonymised, int IncidentsDeleted);

internal sealed class SafetyRetentionSweeper : ISafetyRetentionSweeper
{
    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly SafetyOptions _options;
    private readonly ILogger<SafetyRetentionSweeper> _logger;

    public SafetyRetentionSweeper(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        IOptions<SafetyOptions> options,
        ILogger<SafetyRetentionSweeper> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Read across tenants so the job knows where to go. Every write below happens inside a
    /// per-tenant scope, because <c>EnforceTenantOnWrites</c> rejects saving a tenant-scoped row
    /// whose TenantId is not the ambient one.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var fromArtefacts = await _dbContext.SafetyArtefacts
            .AsNoTracking().IgnoreQueryFilters()
            .Where(a => !a.IsDeleted && a.ExpiresAt <= now)
            .Select(a => a.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromDecisions = await _dbContext.SafetyDecisions
            .AsNoTracking().IgnoreQueryFilters()
            .Where(d => !d.IsDeleted && d.AnonymisedAt == null && d.ExpiresAt <= now)
            .Select(d => d.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. fromArtefacts.Concat(fromDecisions).Distinct()];
    }

    public async Task<SafetySweepSummary> SweepAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var (deleted, held) = await SweepArtefactsAsync(tenantId, now, cancellationToken);
        var anonymised = await AnonymiseDecisionsAsync(tenantId, now, cancellationToken);
        var incidents = await SweepIncidentsAsync(tenantId, now, cancellationToken);

        return new SafetySweepSummary(deleted, held, anonymised, incidents);
    }

    private async Task<(int Deleted, int Held)> SweepArtefactsAsync(
        Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var expired = await _dbContext.SafetyArtefacts
            .Where(a => a.TenantId == tenantId && a.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        var held = expired.Where(a => a.IsUnderLegalHold).ToList();
        var deletable = expired.Where(a => !a.IsUnderLegalHold).ToList();

        foreach (var artefact in held)
        {
            // Logged rather than silently skipped, so preservation and deletion cannot contend
            // invisibly. A hold that quietly stops a sweep is indistinguishable from a broken sweep.
            _logger.LogInformation(
                "Safety artefact {ArtefactId} is past expiry but under legal hold; skipped.", artefact.Id);
        }

        _dbContext.SafetyArtefacts.RemoveRange(deletable);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (deletable.Count, held.Count);
    }

    /// <summary>
    /// Anonymises rather than deletes: the aggregate is what §10.3's evaluation needs, and the
    /// subject link is what minimisation says to drop. Keeping the verdict without the child is the
    /// resolution, not a compromise.
    /// </summary>
    private async Task<int> AnonymiseDecisionsAsync(
        Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var expired = await _dbContext.SafetyDecisions
            .Where(d => d.TenantId == tenantId && d.AnonymisedAt == null && d.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        var heldDecisionIds = await _dbContext.SafetyIncidents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.IsUnderLegalHold)
            .Select(i => i.SafetyDecisionId)
            .ToListAsync(cancellationToken);

        var held = heldDecisionIds.ToHashSet();
        var anonymised = 0;

        foreach (var decision in expired)
        {
            if (held.Contains(decision.Id))
            {
                continue;
            }

            decision.SubjectPartyId = Guid.Empty;
            decision.AnonymisedAt = now;
            anonymised++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return anonymised;
    }

    private async Task<int> SweepIncidentsAsync(
        Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var cutoff = now.AddDays(-_options.IncidentRetentionDays);

        var expired = await _dbContext.SafetyIncidents
            .Where(i => i.TenantId == tenantId && !i.IsUnderLegalHold && i.OccurredAt <= cutoff)
            .ToListAsync(cancellationToken);

        _dbContext.SafetyIncidents.RemoveRange(expired);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }
}
