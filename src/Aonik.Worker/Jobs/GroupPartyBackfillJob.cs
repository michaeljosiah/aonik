using Aonik.Infrastructure.Persistence;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Services;
using Aonik.Platform.Entities.Operations;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Quartz;

namespace Aonik.Worker.Jobs;

/// <summary>
/// Spec 086 P3 — populates the party columns, group kind, resource kind and terms that P2 added.
///
/// A Worker job rather than SQL in the migration body: hand-authoring
/// <c>migrationBuilder.Sql(...)</c> is prohibited by <c>CLAUDE.md</c>, and a data backfill wants what
/// a migration body cannot give it — re-runnability, a report of what it touched, and the ability to
/// refuse rather than guess.
///
/// Idempotent: every query selects only rows that are still unpopulated, so a re-run is safe and is
/// the intended way to pick up rows written between the deployment of P3's dual-writers and the
/// first run.
///
/// The party columns are populated <em>alongside</em> the user columns, never instead of them. The
/// deployed readers still compare the user columns against the authenticated user id, so this job
/// changes nothing observable — that is exactly why it can run before the P5 cutover.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class GroupPartyBackfillJob : IJob
{
    public static readonly JobKey Key = new("GroupPartyBackfillJob", ScheduledJobGroups.ScheduledJobs);

    private readonly AonikDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GroupPartyBackfillJob> _logger;

    public GroupPartyBackfillJob(
        AonikDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<GroupPartyBackfillJob> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;

        var partyByUser = await BuildPartyLookupAsync(ct);
        var tenantIds = await FindTenantsWithWorkAsync(ct);

        // Users the job could not resolve. Kept as (tenant, user) pairs rather than a count so the
        // operator has something to act on — "seven rows failed" is not a fixable report.
        var unresolved = new HashSet<(Guid TenantId, Guid UserId)>();
        var totals = new Totals();

        foreach (var tenantId in tenantIds)
        {
            // The base context refuses to save a tenant-scoped row whose TenantId differs from the
            // ambient tenant, so a cross-tenant backfill cannot commit in one save no matter how it
            // reads. Stamp the tenant, do its rows, commit, reset — the DocumentIngestionBackfillJob
            // pattern, and the only shape that enforcement admits.
            _tenantContext.TenantId = tenantId;
            _tenantContext.ResolutionSource = "backfill";

            try
            {
                totals.Kinds += await BackfillGroupKindsAsync(tenantId, ct);
                totals.Add(await BackfillMembersAsync(tenantId, partyByUser, unresolved, ct));
                totals.Add(await BackfillGrantsAsync(tenantId, partyByUser, unresolved, ct));
                totals.Add(await BackfillInvitesAsync(tenantId, partyByUser, unresolved, ct));

                await _dbContext.SaveChangesAsync(ct);
            }
            finally
            {
                _tenantContext.TenantId = null;
                _tenantContext.ResolutionSource = null;
            }
        }

        _logger.LogInformation(
            "Group party backfill: {Tenants} tenant(s); {Kinds} group kind(s) and {Rows} row(s) populated; "
            + "{Skipped} live row(s) left unresolved.",
            tenantIds.Count, totals.Kinds, totals.Populated, totals.Unresolvable);

        if (unresolved.Count > 0)
        {
            // Loud, not silent. An unresolved row is a member, grant or invite that will simply
            // disappear at the P5 reader cutover, and discovering that when a carer loses access is
            // far worse than discovering it here. The fix is an operator one — link the user to a
            // party — after which this job is re-run.
            var sample = string.Join(", ", unresolved.Take(20).Select(pair => $"{pair.TenantId}/{pair.UserId}"));

            _logger.LogError(
                "Group party backfill could not resolve a party for {Count} user(s). Each leaves at least one live row "
                + "with a null party column, which the Spec 086 P5 reader cutover would drop. Link each user to a party "
                + "(AnkUserParties) or give them a PersonalProfile, then re-run this job. Sample (tenant/user): {Sample}",
                unresolved.Count, sample);

            throw new InvalidOperationException(
                $"{unresolved.Count} user(s) on group rows resolve to no party. See the preceding log entry for the ids.");
        }
    }

    /// <summary>
    /// One pass over both party sources, rather than a query per row.
    /// </summary>
    /// <remarks>
    /// Order matches <see cref="MemberPartyResolver"/> exactly — the <c>AnkUserParties</c> bridge
    /// first, <c>PersonalProfile.PartyId</c> second — because a backfill that resolved differently
    /// from the dual-writer would produce two corpora that disagree, and the disagreement would only
    /// surface at the P5 cutover. The key is (tenant, user), never user alone: resolving on user id
    /// would hand one tenant's member another tenant's party.
    /// </remarks>
    private async Task<Dictionary<(Guid TenantId, Guid UserId), Guid>> BuildPartyLookupAsync(CancellationToken ct)
    {
        var lookup = new Dictionary<(Guid, Guid), Guid>();

        // Profiles first, so the authoritative bridge links overwrite them below.
        // !IsDeleted is explicit because AcrossTenants disables the soft-delete filter along with
        // the tenant one. Without it a deleted profile or a superseded link supplies an obsolete
        // party, the row is reported as resolved rather than raised for repair, and the wrong party
        // is written into a membership that decides who can see what.
        var profiles = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .AcrossTenants()
            .Where(profile => !profile.IsDeleted && profile.PartyId != Guid.Empty)
            .Select(profile => new { profile.TenantId, profile.UserId, profile.PartyId })
            .ToListAsync(ct);

        foreach (var profile in profiles)
        {
            lookup[(profile.TenantId, profile.UserId)] = profile.PartyId;
        }

        var links = await _dbContext.UserParties
            .AsNoTracking()
            .AcrossTenants()
            .Where(link => !link.IsDeleted)
            .OrderBy(link => link.CreatedAt)
            .Select(link => new { link.TenantId, link.UserId, link.PartyId })
            .ToListAsync(ct);

        // Ascending order plus overwrite means the most recent link wins, matching UserPartyResolver.
        foreach (var link in links)
        {
            lookup[(link.TenantId, link.UserId)] = link.PartyId;
        }

        return lookup;
    }

    /// <summary>
    /// Every tenant holding at least one row this job would touch.
    /// </summary>
    /// <remarks>
    /// Derived from the data rather than from the tenant table, so a re-run costs one scan per table
    /// and nothing per idle tenant — and so a tenant whose rows arrived after provisioning (a
    /// restored backup, an import) is not missed.
    /// </remarks>
    private async Task<List<Guid>> FindTenantsWithWorkAsync(CancellationToken ct)
    {
        var fromGroups = await GroupCandidates().Select(item => item.TenantId).Distinct().ToListAsync(ct);
        var fromMembers = await MemberCandidates().Select(item => item.TenantId).Distinct().ToListAsync(ct);
        var fromGrants = await GrantCandidates().Select(item => item.TenantId).Distinct().ToListAsync(ct);
        var fromInvites = await InviteCandidates().Select(item => item.TenantId).Distinct().ToListAsync(ct);

        return fromGroups
            .Concat(fromMembers)
            .Concat(fromGrants)
            .Concat(fromInvites)
            .Distinct()
            .ToList();
    }

    // ── Candidate predicates ────────────────────────────────────────────────────────────────
    //
    // Each selects only rows still missing something, which is what makes the job idempotent: a
    // second run finds nothing, and so cannot overwrite what P3's dual-writers already put there.
    //
    // IncludeSoftDeleted is deliberate. A deleted row still occupies its index slot and would come
    // back with a null party if ever restored, so it is worth populating — it simply must not
    // *block* the job (see Record). That call disables the tenant filter too, which is why every
    // per-tenant query below carries its own explicit TenantId predicate.

    private IQueryable<Household> GroupCandidates()
        => _dbContext.Households.IncludeSoftDeleted()
            .Where(group => group.Kind == null || group.Kind == "");

    private IQueryable<HouseholdMember> MemberCandidates()
        => _dbContext.HouseholdMembers.IncludeSoftDeleted()
            .Where(member => member.PartyId == null && member.UserId != null);

    private IQueryable<CircleGrant> GrantCandidates()
        => _dbContext.CircleGrants.IncludeSoftDeleted()
            .Where(grant => grant.OwnerPartyId == null
                || (grant.MemberUserId != null && grant.MemberPartyId == null)
                || grant.ResourceKind == null || grant.ResourceKind == ""
                || grant.TermsJson == null);

    private IQueryable<CircleInvite> InviteCandidates()
        => _dbContext.CircleInvites.IncludeSoftDeleted()
            .Where(invite => invite.OwnerPartyId == null
                || invite.ResourceKind == null || invite.ResourceKind == ""
                || invite.TermsJson == null);

    // ── Per-tenant backfills ────────────────────────────────────────────────────────────────

    private async Task<int> BackfillGroupKindsAsync(Guid tenantId, CancellationToken ct)
    {
        var groups = await GroupCandidates().Where(group => group.TenantId == tenantId).ToListAsync(ct);

        foreach (var group in groups)
        {
            // Everything that exists before Spec 086 was created by PersonalFinance, and every one
            // of those is a household. A family is what the other product creates from P4 onward.
            group.Kind = GroupKinds.Household;
        }

        return groups.Count;
    }

    private async Task<Totals> BackfillMembersAsync(
        Guid tenantId,
        Dictionary<(Guid, Guid), Guid> partyByUser,
        HashSet<(Guid, Guid)> unresolved,
        CancellationToken ct)
    {
        var members = await MemberCandidates().Where(member => member.TenantId == tenantId).ToListAsync(ct);
        var totals = new Totals();

        foreach (var member in members)
        {
            if (partyByUser.TryGetValue((member.TenantId, member.UserId!.Value), out var partyId))
            {
                member.PartyId = partyId;
                totals.Populated++;
                continue;
            }

            totals.Unresolvable++;
            Record(unresolved, member.IsDeleted, member.TenantId, member.UserId.Value);
        }

        return totals;
    }

    private async Task<Totals> BackfillGrantsAsync(
        Guid tenantId,
        Dictionary<(Guid, Guid), Guid> partyByUser,
        HashSet<(Guid, Guid)> unresolved,
        CancellationToken ct)
    {
        var grants = await GrantCandidates().Where(grant => grant.TenantId == tenantId).ToListAsync(ct);
        var totals = new Totals();

        foreach (var grant in grants)
        {
            if (string.IsNullOrWhiteSpace(grant.ResourceKind))
            {
                // EntityIdsJson has only ever held care-entity ids, so the kind is not a guess.
                grant.ResourceKind = ShareResourceKinds.CareEntity;
            }

            grant.TermsJson ??= CircleGrantTerms.Serialize(grant.NoAmounts);

            var resolvedAll = true;

            if (grant.OwnerPartyId is null)
            {
                if (partyByUser.TryGetValue((grant.TenantId, grant.OwnerUserId), out var ownerPartyId))
                {
                    grant.OwnerPartyId = ownerPartyId;
                }
                else
                {
                    resolvedAll = false;
                    Record(unresolved, grant.IsDeleted, grant.TenantId, grant.OwnerUserId);
                }
            }

            // A pending grant has no member yet. That is not an unresolvable row — treating it as
            // one would make the job fail on every outstanding invite.
            if (grant.MemberPartyId is null && grant.MemberUserId is { } memberUserId)
            {
                if (partyByUser.TryGetValue((grant.TenantId, memberUserId), out var memberPartyId))
                {
                    grant.MemberPartyId = memberPartyId;
                }
                else
                {
                    resolvedAll = false;
                    Record(unresolved, grant.IsDeleted, grant.TenantId, memberUserId);
                }
            }

            if (resolvedAll)
            {
                totals.Populated++;
            }
            else
            {
                totals.Unresolvable++;
            }
        }

        return totals;
    }

    private async Task<Totals> BackfillInvitesAsync(
        Guid tenantId,
        Dictionary<(Guid, Guid), Guid> partyByUser,
        HashSet<(Guid, Guid)> unresolved,
        CancellationToken ct)
    {
        var invites = await InviteCandidates().Where(invite => invite.TenantId == tenantId).ToListAsync(ct);
        var totals = new Totals();

        foreach (var invite in invites)
        {
            if (string.IsNullOrWhiteSpace(invite.ResourceKind))
            {
                invite.ResourceKind = ShareResourceKinds.CareEntity;
            }

            invite.TermsJson ??= CircleGrantTerms.Serialize(invite.NoAmounts);

            if (invite.OwnerPartyId is null)
            {
                if (partyByUser.TryGetValue((invite.TenantId, invite.OwnerUserId), out var ownerPartyId))
                {
                    invite.OwnerPartyId = ownerPartyId;
                }
                else
                {
                    totals.Unresolvable++;
                    Record(unresolved, invite.IsDeleted, invite.TenantId, invite.OwnerUserId);
                    continue;
                }
            }

            totals.Populated++;
        }

        return totals;
    }

    /// <summary>
    /// Records an unresolvable user, unless the row carrying it is soft-deleted.
    /// </summary>
    /// <remarks>
    /// A member deleted years ago whose user has since been purged is not a defect anyone can fix,
    /// and a backfill that can never go green is a backfill whose failure everyone learns to ignore.
    /// </remarks>
    private static void Record(HashSet<(Guid, Guid)> unresolved, bool isDeleted, Guid tenantId, Guid userId)
    {
        if (!isDeleted)
        {
            unresolved.Add((tenantId, userId));
        }
    }

    private sealed class Totals
    {
        public int Kinds;
        public int Populated;
        public int Unresolvable;

        public void Add(Totals other)
        {
            Populated += other.Populated;
            Unresolvable += other.Unresolvable;
        }
    }
}
