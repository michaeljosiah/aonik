using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Consent;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.Platform.Entities.Party;
using Aonik.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Consent;

/// <summary>
/// Spec 095 §11 — the two age transitions, and the safety-band moves alongside them.
///
/// <para>
/// The transition most products never build, and are therefore quietly wrong about. Guardian
/// authority is temporary by nature; a product that grants it at sign-up and never ends it is
/// asserting, indefinitely, that an adult may read a grown person's private work because they were
/// once a child. That is a continuing unlawful basis, and it accrues silently.
/// </para>
///
/// <para>
/// Every step here is idempotent, which is what makes running it on a cron safe rather than merely
/// convenient: transitions are driven off stored dates and a re-run finds nothing left to do.
/// </para>
/// </summary>
internal sealed class AgeTransitionService
{
    /// <summary>How far ahead notice is given, so a transition is expected rather than abrupt.</summary>
    private static readonly TimeSpan NoticeWindow = TimeSpan.FromDays(30);

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;
    private readonly ILogger<AgeTransitionService> _logger;

    public AgeTransitionService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock,
        ILogger<AgeTransitionService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
        _logger = logger;
    }

    public sealed record TransitionSummary(
        int NoticesGiven, int ConsentAgeReached, int MajorityReached, int SafetyBandsChanged);

    /// <summary>
    /// Tenants with any party whose stored dates put work in scope. Queried across tenants so the
    /// job knows where to go; every WRITE below happens inside a per-tenant scope, because
    /// <c>EnforceTenantOnWrites</c> rejects saving a tenant-scoped row whose TenantId is not the
    /// ambient one.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> FindTenantsWithWorkAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var noticeHorizon = now.Add(NoticeWindow);

        return await _dbContext.Parties
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted
                && ((p.ConsentAgeOn != null && p.ConsentAgeOn <= noticeHorizon)
                    || (p.MajorityOn != null && p.MajorityOn <= noticeHorizon)
                    || (p.SafetyBandChangesOn != null && p.SafetyBandChangesOn <= now)))
            .Select(p => p.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<TransitionSummary> RunAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var now = _clock.UtcNow;

        var notices = await GiveNoticeAsync(tenantId, now, cancellationToken);
        var consentAge = await ApplyConsentAgeAsync(tenantId, now, cancellationToken);
        var majority = await ApplyMajorityAsync(tenantId, now, cancellationToken);
        var bands = await ApplySafetyBandsAsync(tenantId, now, cancellationToken);

        return new TransitionSummary(notices, consentAge, majority, bands);
    }

    /// <summary>
    /// §11: both parties are notified <em>in advance</em>. Idempotent through
    /// <c>AgeTransitionNoticeSentOn</c> — without a marker a daily cron would notify every day for a
    /// month, which is how a considerate feature becomes a nuisance.
    /// </summary>
    private async Task<int> GiveNoticeAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var horizon = now.Add(NoticeWindow);

        var due = await _dbContext.Parties
            .Where(p => p.TenantId == tenantId
                && p.AgeTransitionNoticeSentOn == null
                && ((p.ConsentAgeOn != null && p.ConsentAgeOn > now && p.ConsentAgeOn <= horizon)
                    || (p.MajorityOn != null && p.MajorityOn > now && p.MajorityOn <= horizon)))
            .ToListAsync(cancellationToken);

        foreach (var party in due)
        {
            var isConsentAge = party.ConsentAgeOn is { } consentOn && consentOn > now && consentOn <= horizon;
            var occursOn = isConsentAge ? party.ConsentAgeOn!.Value : party.MajorityOn!.Value;

            _dbContext.EnqueueIntegrationEvent(new AgeTransitionApproachingEvent(
                tenantId,
                party.Id,
                await GuardiansOfAsync(tenantId, party.Id, cancellationToken),
                isConsentAge ? AgeTransitionKinds.ConsentAge : AgeTransitionKinds.Majority,
                occursOn));

            party.AgeTransitionNoticeSentOn = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    /// <summary>
    /// §11.2. Guardian <em>consents</em> lapse; the <c>Guardian</c> edge does <strong>not</strong>.
    ///
    /// <para>
    /// Collapsing these was the defect §11.1 exists to prevent: in the UK a 13-year-old may consent
    /// to data processing while remaining under guardianship for another five years, so ending the
    /// edge here would strip a parent's authority over their own 14-year-old.
    /// </para>
    /// </summary>
    private async Task<int> ApplyConsentAgeAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var due = await _dbContext.Parties
            .Where(p => p.TenantId == tenantId
                && p.ConsentAgeOn != null
                && p.ConsentAgeOn <= now
                && p.ConsentBand == PartyConsentBands.BelowThreshold)
            .ToListAsync(cancellationToken);

        foreach (var party in due)
        {
            // Guardian grants only. A self-grant the young person has already made is untouched —
            // grantor == subject is the marker (§11.3), and lapsing their own consent would be
            // absurd on the day they acquire the right to give it.
            var guardianGrants = await _dbContext.ConsentGrants
                .Where(g => g.TenantId == tenantId
                    && g.SubjectPartyId == party.Id
                    && g.RevokedAt == null
                    && g.GrantedByPartyId != party.Id)
                .ToListAsync(cancellationToken);

            foreach (var grant in guardianGrants)
            {
                grant.RevokedAt = now;
                grant.RevocationReason = ConsentRevocationReasons.AgeUpLapse;
            }

            party.ConsentBand = PartyConsentBands.SelfConsenting;

            _dbContext.EnqueueIntegrationEvent(new ConsentAgeReachedEvent(
                tenantId, party.Id, [.. guardianGrants.Select(g => g.Purpose).Distinct()]));

            _logger.LogInformation(
                "Party {PartyId} reached consent age; {Count} guardian grants lapsed, guardian edge retained until majority.",
                party.Id, guardianGrants.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    /// <summary>
    /// §11.1. The edge itself ends. This is the only place guardian authority is deactivated by the
    /// passage of time.
    /// </summary>
    private async Task<int> ApplyMajorityAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var due = await _dbContext.Parties
            .Where(p => p.TenantId == tenantId
                && p.MajorityOn != null
                && p.MajorityOn <= now
                && p.ConsentBand != PartyConsentBands.Adult)
            .ToListAsync(cancellationToken);

        foreach (var party in due)
        {
            var edges = await _dbContext.PartyRelationships
                .Where(r => r.TenantId == tenantId
                    && r.ToPartyId == party.Id
                    && r.IsActive
                    && r.RelationshipTypeCode == PartyRelationshipTypes.Guardian)
                .ToListAsync(cancellationToken);

            foreach (var edge in edges)
            {
                edge.IsActive = false;
            }

            // Any guardian grant still standing lapses here too. In practice consent age has already
            // taken them, but a party enrolled after that date would otherwise keep one.
            var remaining = await _dbContext.ConsentGrants
                .Where(g => g.TenantId == tenantId
                    && g.SubjectPartyId == party.Id
                    && g.RevokedAt == null
                    && g.GrantedByPartyId != party.Id)
                .ToListAsync(cancellationToken);

            foreach (var grant in remaining)
            {
                grant.RevokedAt = now;
                grant.RevocationReason = ConsentRevocationReasons.AgeUpLapse;
            }

            party.ConsentBand = PartyConsentBands.Adult;
            party.SafetyBand = PartySafetyBands.Adult;
            party.SafetyBandChangesOn = null;

            _dbContext.EnqueueIntegrationEvent(new MajorityReachedEvent(
                tenantId, party.Id, [.. edges.Select(e => e.FromPartyId).Distinct()]));

            // Deliberately NOT done here: transferring workspace ownership. §12 makes the child the
            // owner from creation, so there is nothing to transfer — age-up is an authority change.
            // Invoking Spec 089's transfer path would also migrate billing claims and can refuse on
            // capacity, which could lock a young person out of their own childhood work on the day
            // they became entitled to it. Billing moves separately, and access never pauses.
            _logger.LogInformation(
                "Party {PartyId} reached majority; {Count} guardian edges deactivated.",
                party.Id, edges.Count);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    /// <summary>
    /// Spec 096 §9. Safety banding tracks minority, not consent capacity — so it moves on its own
    /// dates and keeps moving after the consent threshold.
    /// </summary>
    private async Task<int> ApplySafetyBandsAsync(Guid tenantId, DateTime now, CancellationToken cancellationToken)
    {
        var due = await _dbContext.Parties
            .Where(p => p.TenantId == tenantId
                && p.SafetyBandChangesOn != null
                && p.SafetyBandChangesOn <= now
                && p.BirthYear != null)
            .ToListAsync(cancellationToken);

        var changed = 0;

        foreach (var party in due)
        {
            var previous = party.SafetyBand ?? PartySafetyBands.Default;
            var next = NextBandAfter(previous);

            if (next is null)
            {
                party.SafetyBandChangesOn = null;
                continue;
            }

            party.SafetyBand = next;
            party.SafetyBandChangesOn = NextChangeAfter(party, now);

            _dbContext.EnqueueIntegrationEvent(new SafetyBandChangedEvent(
                tenantId, party.Id, previous, next));

            changed++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private static string? NextBandAfter(string current)
    {
        var order = PartySafetyBands.Boundaries.Select(b => b.Band).ToList();
        var index = order.IndexOf(current);

        return index >= 0 && index < order.Count - 1 ? order[index + 1] : null;
    }

    /// <summary>
    /// The next boundary after <paramref name="now"/>, derived from the stored dates rather than
    /// from a birth year — a year cannot say when a child turns 6, 10 or 13, and guessing sends a
    /// December-born child into a looser band eleven months early.
    /// </summary>
    private static DateTime? NextChangeAfter(PartyEntity party, DateTime now)
    {
        if (party.ConsentAgeOn is { } consentOn && consentOn > now)
        {
            return consentOn;
        }

        return party.MajorityOn is { } majorityOn && majorityOn > now ? majorityOn : null;
    }

    private async Task<IReadOnlyList<Guid>> GuardiansOfAsync(
        Guid tenantId, Guid childPartyId, CancellationToken cancellationToken)
        => await _dbContext.PartyRelationships
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId
                && r.ToPartyId == childPartyId
                && r.IsActive
                && r.RelationshipTypeCode == PartyRelationshipTypes.Guardian)
            .Select(r => r.FromPartyId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
