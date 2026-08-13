using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
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

/// <param name="HoldsExpired">
/// Pre-review holds nobody acted on, moved to <c>Expired</c>. On the scheduled path rather than on a
/// later approval attempt: a hold that is merely hidden from the guardian's queue is still a row
/// carrying a child's content reference, and rows nobody ever returns to would never resolve at all.
/// </param>
public sealed record SafetySweepSummary(
    int ArtefactsDeleted,
    int ArtefactsHeld,
    int DecisionsAnonymised,
    int IncidentsDeleted,
    int HoldsExpired);

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
            .AsNoTracking().AcrossTenants()
            .Where(a => !a.IsDeleted && a.ExpiresAt <= now)
            .Select(a => a.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromDecisions = await _dbContext.SafetyDecisions
            .AsNoTracking().AcrossTenants()
            .Where(d => !d.IsDeleted && d.AnonymisedAt == null && d.ExpiresAt <= now)
            .Select(d => d.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var fromHolds = await _dbContext.PendingContentReviews
            .AsNoTracking().AcrossTenants()
            .Where(r => !r.IsDeleted
                && r.State == Entities.Safety.PreReviewStates.Pending
                && r.ExpiresAt <= now)
            .Select(r => r.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. fromArtefacts.Concat(fromDecisions).Concat(fromHolds).Distinct()];
    }

    public async Task<SafetySweepSummary> SweepAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var (deleted, held) = await SweepArtefactsAsync(tenantId, now, cancellationToken);
        var anonymised = await AnonymiseDecisionsAsync(tenantId, now, cancellationToken);
        var incidents = await SweepIncidentsAsync(tenantId, now, cancellationToken);
        var holdsExpired = await ExpireUnattendedHoldsAsync(tenantId, now, cancellationToken);

        return new SafetySweepSummary(deleted, held, anonymised, incidents, holdsExpired);
    }

    /// <summary>
    /// Resolves pre-review holds nobody acted on (§8).
    ///
    /// <para>
    /// Resolved as <c>Expired</c> and never as approval — an unattended queue must not become an
    /// approval mechanism. Doing it here rather than on the next decision attempt is the difference
    /// between a finite window and a growing table of undelivered children's stories that nobody will
    /// ever open again.
    /// </para>
    /// </summary>
    private async Task<int> ExpireUnattendedHoldsAsync(
        Guid tenantId, DateTime now, CancellationToken cancellationToken)
        => await _dbContext.PendingContentReviews
            .Where(r => r.TenantId == tenantId
                && r.State == Entities.Safety.PreReviewStates.Pending
                && r.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(r => r.State, Entities.Safety.PreReviewStates.Expired),
                cancellationToken);

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

        // ExecuteDeleteAsync, NOT RemoveRange — and this is the whole point of the method.
        //
        // AonikDbContextBase turns every EntityState.Deleted into IsDeleted = true, which is correct
        // for ordinary business data (AiModelService says so in as many words) and exactly wrong
        // here. A retention sweep that marks blocked child content instead of removing it retains it
        // forever, while reporting that it deleted something — the precise failure §13 exists to
        // prevent, and one that would have shipped looking like it worked.
        //
        // ExecuteDelete bypasses the change tracker, so the interceptor never sees the entity and
        // the rows are actually gone.
        var deleted = deletable.Count == 0
            ? 0
            : await _dbContext.SafetyArtefacts
                .Where(a => a.TenantId == tenantId && a.ExpiresAt <= now && !a.IsUnderLegalHold)
                .ExecuteDeleteAsync(cancellationToken);

        return (deleted, held.Count);
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

        // Hard delete, for the same reason as the artefacts above: a soft-deleted incident is a
        // retained record about a child that every later sweep counts again.
        return await _dbContext.SafetyIncidents
            .Where(i => i.TenantId == tenantId && !i.IsUnderLegalHold && i.OccurredAt <= cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
