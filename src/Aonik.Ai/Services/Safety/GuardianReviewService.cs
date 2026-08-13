using Aonik.Ai.Entities.Safety;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Safety;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services.Safety;

/// <summary>
/// Spec 096 §8 — guardian visibility, appeal, and the branch no guardian may cross.
///
/// <para>
/// Arke Studio's accept gate does not transfer here, and that is worth stating because reusing it is
/// the obvious move. In Studio the human reviewing a proposal is the author protecting their own
/// record; in Kids the recipient is <strong>the person being protected</strong>, so putting the child
/// in the review seat means showing them the thing they were shielded from and asking them to judge
/// it. The human layer is therefore the guardian — and deliberately secondary, because a parent is
/// not a moderation queue and will approve in bulk within a week.
/// </para>
/// </summary>
public interface IGuardianReviewService
{
    /// <summary>Incidents for a child, for the adult who may see them.</summary>
    Task<IReadOnlyList<GuardianVisibleIncident>> ListForGuardianAsync(
        Guid guardianPartyId,
        Guid childPartyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// A guardian's decision on a blocked item.
    ///
    /// <para>
    /// On a <strong>non-overridable</strong> category this does not release anything. The appeal is
    /// recorded as a <em>signal</em>, and repeated appeals against that category on one account
    /// escalate to a person — a guardian account is not proof of good intent, and this is exactly
    /// where that matters.
    /// </para>
    /// </summary>
    Task<AppealOutcome> AppealAsync(
        Guid guardianPartyId,
        Guid incidentId,
        CancellationToken cancellationToken = default);
}

/// <param name="CanView">False for a non-overridable category — the guardian may not see it either.</param>
/// <param name="CanRelease">False unless the category is reviewable and undecided.</param>
/// <param name="ArtefactReference">
/// Where the withheld content is, so a guardian who <see cref="CanView"/> can actually look at it.
/// <strong>Null whenever <see cref="CanView"/> is false</strong>, and null once the short appeal
/// window has closed — a viewable flag with nothing behind it is a review flow that does not work.
/// </param>
public sealed record GuardianVisibleIncident(
    Guid IncidentId,
    string Category,
    DateTime OccurredAt,
    string AppealState,
    bool CanView,
    bool CanRelease,
    string? ArtefactReference);

public enum AppealOutcome
{
    /// <summary>Reviewable category, released to that guardian's own child.</summary>
    Released = 0,

    /// <summary>Non-overridable. Recorded, never released, and counted toward escalation.</summary>
    Refused = 1,

    /// <summary>Nothing to appeal — already decided, or the artefact has expired.</summary>
    NotAvailable = 2,
}

internal sealed class GuardianReviewService : IGuardianReviewService
{
    /// <summary>Appeals against a non-overridable category before a person is told.</summary>
    private const int EscalationThreshold = 3;

    private readonly AiDbContext _dbContext;
    private readonly IGuardianshipReader _guardianship;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<GuardianReviewService> _logger;

    public GuardianReviewService(
        AiDbContext dbContext,
        IGuardianshipReader guardianship,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<GuardianReviewService> logger)
    {
        _dbContext = dbContext;
        _guardianship = guardianship;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GuardianVisibleIncident>> ListForGuardianAsync(
        Guid guardianPartyId, Guid childPartyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // Guardian authority, from Spec 095. Kinship grants nothing, and an edge past majority is
        // already refused by the reader — so a parent cannot browse their adult child's records.
        if (!await _guardianship.HasAuthorityAsync(tenantId, guardianPartyId, childPartyId, cancellationToken))
        {
            throw new GuardianAuthorityRequiredException(guardianPartyId, childPartyId);
        }

        var incidents = await _dbContext.SafetyIncidents
            .AsNoTracking()
            .Where(i => i.TenantId == tenantId && i.SubjectPartyId == childPartyId)
            .OrderByDescending(i => i.OccurredAt)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var incidentIds = incidents.Select(i => i.Id).ToList();

        // Only live artefacts. An expired one is gone, and reporting a reference to it would hand the
        // guardian a link to nothing.
        var artefacts = await _dbContext.SafetyArtefacts
            .AsNoTracking()
            .Where(a => a.TenantId == tenantId
                && incidentIds.Contains(a.SafetyIncidentId)
                && a.ExpiresAt > now)
            .ToDictionaryAsync(a => a.SafetyIncidentId, a => a.Reference, cancellationToken);

        return
        [
            .. incidents.Select(i =>
            {
                var reviewable = IsReviewable(i);
                var reference = artefacts.GetValueOrDefault(i.Id);

                return new GuardianVisibleIncident(
                    i.Id,
                    i.Category,
                    i.OccurredAt,
                    i.AppealState,
                    // A non-overridable incident is listed — the guardian is told it happened — but
                    // its content is NOT viewable. Telling them nothing would be its own failure;
                    // showing them sexual content involving their child would be another.
                    CanView: reviewable && reference is not null,
                    CanRelease: reviewable && reference is not null
                        && i.AppealState == SafetyAppealStates.None,
                    // Never populated for a category they may not see, whatever a caller does with
                    // the flags.
                    ArtefactReference: reviewable ? reference : null);
            })
        ];
    }

    /// <summary>
    /// Whether a guardian may see and release this at all.
    ///
    /// <para>
    /// <strong>An unrecognised category is treated as non-overridable.</strong> The policy reader
    /// blocks unknown categories at a low threshold precisely so a classifier that grows a new label
    /// does not become silently unenforced — and deciding releasability from set membership alone
    /// would undo that at the appeal layer, handing the guardian exactly the labels nobody has
    /// classified yet. Silence is not permission here either.
    /// </para>
    /// </summary>
    private static bool IsReviewable(SafetyIncident incident)
        => !incident.IsNonOverridable
            && !incident.IsUnderLegalHold
            && SafetyCategories.All.Contains(incident.Category);

    public async Task<AppealOutcome> AppealAsync(
        Guid guardianPartyId, Guid incidentId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var incident = await _dbContext.SafetyIncidents
            .FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == incidentId, cancellationToken);

        if (incident is null)
        {
            return AppealOutcome.NotAvailable;
        }

        if (!await _guardianship.HasAuthorityAsync(
                tenantId, guardianPartyId, incident.SubjectPartyId, cancellationToken))
        {
            throw new GuardianAuthorityRequiredException(guardianPartyId, incident.SubjectPartyId);
        }

        var now = _clock.UtcNow;

        // The state check comes BEFORE either branch. Running it only on the reviewable side let a
        // retry — or a second authorised guardian — overwrite who first appealed a sealed incident
        // and when, erasing the record the escalation is built on.
        if (incident.AppealState != SafetyAppealStates.None)
        {
            return AppealOutcome.NotAvailable;
        }

        if (!IsReviewable(incident))
        {
            // Recorded, never released. Denormalised at write time (S1) precisely so a later policy
            // edit cannot retroactively make a sealed incident releasable.
            incident.AppealState = SafetyAppealStates.Refused;
            incident.AppealDecidedByPartyId = guardianPartyId;
            incident.AppealDecidedAt = now;
            await _dbContext.SaveChangesAsync(cancellationToken);

            await EscalateIfRepeatedAsync(tenantId, guardianPartyId, cancellationToken);
            return AppealOutcome.Refused;
        }

        // The artefact is short-lived by design, so an appeal can expire. That is the intended
        // trade: keeping the very thing we judged unsafe, indefinitely, would be perverse.
        var artefactExists = await _dbContext.SafetyArtefacts
            .AsNoTracking()
            .AnyAsync(a => a.TenantId == tenantId
                && a.SafetyIncidentId == incident.Id
                && a.ExpiresAt > now, cancellationToken);

        if (!artefactExists)
        {
            return AppealOutcome.NotAvailable;
        }

        incident.AppealState = SafetyAppealStates.Released;
        incident.AppealDecidedByPartyId = guardianPartyId;
        incident.AppealDecidedAt = now;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Recorded as a GUARDIAN DECISION, not as a false-positive label. §10.3 says a parent's
        // judgement may outrank an ACCURATE classification, so a release says "this guardian chose to
        // allow it" and not "the classifier was wrong". Feeding these into tuning would teach the
        // system to pass content reviewers still consider unsafe, one permissive parent at a time,
        // with the drift showing up as an improving metric. A safety system that learns from the
        // people it constrains converges on constraining nobody.
        _logger.LogInformation(
            "Guardian {GuardianId} released incident {IncidentId} ({Category}) to their own child. "
            + "Recorded as a decision; a false-positive label requires independent review.",
            guardianPartyId, incident.Id, incident.Category);

        return AppealOutcome.Released;
    }

    /// <summary>
    /// Counted per <strong>guardian account</strong>, not per child.
    ///
    /// <para>
    /// §8's concern is a pattern of attempts by one adult, and grouping by the protected child gets it
    /// wrong in both directions: a guardian appealing one sealed incident for each of several wards
    /// never reaches the threshold, while two different guardians appealing about the same child are
    /// wrongly combined into one accusation. The identity that matters is the one on
    /// <c>AppealDecidedByPartyId</c>.
    /// </para>
    /// </summary>
    private async Task EscalateIfRepeatedAsync(
        Guid tenantId, Guid guardianPartyId, CancellationToken cancellationToken)
    {
        var refusals = await _dbContext.SafetyIncidents
            .AsNoTracking()
            .CountAsync(i => i.TenantId == tenantId
                && i.AppealDecidedByPartyId == guardianPartyId
                && i.AppealState == SafetyAppealStates.Refused, cancellationToken);

        if (refusals < EscalationThreshold)
        {
            return;
        }

        // An appeal against a non-overridable category is a signal rather than a request, and a
        // pattern of them is the thing §11's written staff route exists to receive. Logged at
        // warning rather than automated further: we are not equipped to act on it in code.
        _logger.LogWarning(
            "Guardian {GuardianId} has {Count} refused appeals against non-overridable categories; "
            + "escalating to the named responsible person per Spec 096 §11/§12.",
            guardianPartyId, refusals);
    }
}
