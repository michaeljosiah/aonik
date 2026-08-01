using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// Everything a group membership means to PersonalFinance (Spec 086 §7.1).
/// </summary>
/// <remarks>
/// <para>
/// This is the other half of the old <c>HouseholdService</c> — the half that is not about groups at
/// all. A household membership carries a profile link, a set of shared accounts, an integration
/// event and a cached life graph, and none of that belongs on a platform entity. Moving the
/// lifecycle behind a veto-only interface would have silently dropped every one of them, which is
/// why the seam both refuses and reacts.
/// </para>
/// <para>
/// It writes through <c>PersonalFinanceDbContext</c>, which is the same instance <c>GroupService</c>
/// writes through — see <c>IGroupDataContext</c>. That is what makes "same transaction" real: the
/// membership flip and the profile unlink land in one <c>SaveChangesAsync</c>, so a crash cannot
/// leave a member removed but still linked to the household they left.
/// </para>
/// <para>
/// A transition whose <c>MemberUserId</c> is null is <b>skipped</b>, not failed. A child added
/// directly to a family has no personal-finance profile to link and no accounts to unshare; there
/// is nothing here that applies to them, and treating that as an error would make Arke Kids
/// unusable on a deployment that happens to include this module.
/// </para>
/// </remarks>
internal sealed class PersonalFinanceGroupLifecycleContributor : IGroupLifecycleContributor
{
    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IClock _clock;

    public PersonalFinanceGroupLifecycleContributor(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        IClock clock)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _clock = clock;
    }

    public string ModuleName => "PersonalFinance";

    public async Task<string?> VetoAsync(GroupTransition transition, CancellationToken cancellationToken = default)
    {
        if (!IsHousehold(transition) || transition.MemberUserId is not { } userId)
        {
            return null;
        }

        return transition.Kind switch
        {
            GroupTransitionKinds.Created => await VetoJoinAsync(userId, null, cancellationToken),
            GroupTransitionKinds.InviteAccepted => await VetoJoinAsync(userId, transition.GroupId, cancellationToken),

            // An invitation is not yet a membership, so the exclusivity rule does not apply — but
            // inviting someone who already belongs elsewhere would mint an invitation they could
            // never accept, and the endpoint has always refused it up front.
            GroupTransitionKinds.MemberInvited => await VetoInviteAsync(userId, transition.GroupId, cancellationToken),
            _ => null
        };
    }

    /// <summary>
    /// One household per user — the rule that reads as generic and is not.
    /// </summary>
    /// <remarks>
    /// A personal-finance household is exclusive because a user's accounts, budgets and life graph
    /// hang off exactly one of them. Groups in general are not: a child of separated parents belongs
    /// to two families, and the Spec 086 index change exists to permit precisely that. So this lives
    /// here, as a veto, rather than in <c>GroupService</c> where it would quietly impose finance's
    /// shape on every future product.
    /// </remarks>
    private async Task<string?> VetoJoinAsync(Guid userId, Guid? joiningGroupId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var profile = await FindProfileAsync(tenantId, userId, cancellationToken);
        if (profile is null)
        {
            return "Personal profile is required to manage household membership.";
        }

        if (profile.HouseholdId.HasValue && profile.HouseholdId != joiningGroupId)
        {
            return "User already belongs to a household.";
        }

        if (await BelongsToAnotherHouseholdAsync(tenantId, userId, joiningGroupId, cancellationToken))
        {
            return "User already belongs to a household.";
        }

        return null;
    }

    /// <summary>
    /// Whether this user is an accepted member of some <b>other household</b>.
    /// </summary>
    /// <remarks>
    /// The group filter is the load-bearing part. Scanning memberships by user alone counts a
    /// <em>family</em> as a household, so a parent in an Arke Kids family could not create or join
    /// their first personal-finance household at all — this module's exclusivity rule reaching
    /// across into another product's groups, which is the same coupling the transition-kind guard
    /// removes from the other direction.
    /// </remarks>
    private async Task<bool> BelongsToAnotherHouseholdAsync(
        Guid tenantId,
        Guid userId,
        Guid? joiningGroupId,
        CancellationToken cancellationToken)
    {
        var memberships = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .Join(
                _dbContext.Households.AsNoTracking().Where(group => group.TenantId == tenantId),
                member => member.HouseholdId,
                group => group.Id,
                (member, group) => new { Member = member, group.Kind })
            .ToListAsync(cancellationToken);

        foreach (var row in memberships)
        {
            // Empty counts as household: every group written before Spec 086 has one.
            var isHousehold = string.IsNullOrEmpty(row.Kind)
                || string.Equals(row.Kind, GroupKinds.Household, StringComparison.OrdinalIgnoreCase);

            if (!isHousehold)
            {
                continue;
            }

            HouseholdMembershipRules.NormalizeLegacyMember(row.Member);

            if (HouseholdMembershipRules.IsAccepted(row.Member) && row.Member.HouseholdId != joiningGroupId)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<string?> VetoInviteAsync(Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        if (await FindProfileAsync(tenantId, userId, cancellationToken) is null)
        {
            // Also the existence check: a user with no personal profile is either unknown or not a
            // PersonalFinance user, and the endpoint has always reported both the same way.
            return "User not found.";
        }

        return await BelongsToAnotherHouseholdAsync(tenantId, userId, groupId, cancellationToken)
            ? "User already belongs to a household."
            : null;
    }

    public async Task OnCommittedAsync(GroupTransition transition, CancellationToken cancellationToken = default)
    {
        if (!IsHousehold(transition) || transition.MemberUserId is not { } userId)
        {
            return;
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();

        switch (transition.Kind)
        {
            case GroupTransitionKinds.Created:
                await LinkProfileAsync(tenantId, userId, transition.GroupId, cancellationToken);
                _dbContext.EnqueueIntegrationEvent(new HouseholdCreatedEvent(tenantId, transition.GroupId, userId));
                break;

            case GroupTransitionKinds.MemberInvited:
                _dbContext.EnqueueIntegrationEvent(new HouseholdMemberInvitedEvent(
                    tenantId,
                    transition.GroupId,
                    userId,
                    transition.ActorUserId ?? Guid.Empty,
                    // From the transition, not the table: this runs before the group service saves,
                    // so a projection would miss a new membership entirely and read the stale role
                    // off a reused invitation row.
                    transition.Role ?? HouseholdRoles.Viewer));
                break;

            case GroupTransitionKinds.InviteDeclined:
                _dbContext.EnqueueIntegrationEvent(new HouseholdInvitationDeclinedEvent(
                    tenantId, transition.GroupId, userId));
                break;

            case GroupTransitionKinds.InviteAccepted:
                await LinkProfileAsync(tenantId, userId, transition.GroupId, cancellationToken);
                await DeclineOtherPendingInvitationsAsync(tenantId, userId, transition.GroupId, cancellationToken);
                _dbContext.EnqueueIntegrationEvent(new HouseholdInvitationAcceptedEvent(tenantId, transition.GroupId, userId));
                break;

            case GroupTransitionKinds.MemberRemoved:
                await UnlinkAsync(tenantId, userId, transition.GroupId, cancellationToken);

                // Left vs removed is not cosmetic — the two events drive different notifications and
                // a different audit story. Matched on either identifier: a membership written before
                // the P3 backfill has no party, so comparing parties alone reads the member's own
                // departure as somebody else removing them, on precisely the environments where the
                // disabled-by-default backfill has not run.
                if (IsSelfRemoval(transition))
                {
                    _dbContext.EnqueueIntegrationEvent(new HouseholdMemberLeftEvent(tenantId, transition.GroupId, userId));
                }
                else
                {
                    _dbContext.EnqueueIntegrationEvent(new HouseholdMemberRemovedEvent(
                        tenantId, transition.GroupId, userId, transition.ActorUserId ?? Guid.Empty));
                }

                break;

            case GroupTransitionKinds.OwnershipTransferred:
                _dbContext.EnqueueIntegrationEvent(new HouseholdOwnershipTransferredEvent(
                    tenantId, transition.GroupId, transition.ActorUserId ?? Guid.Empty, userId));
                break;
        }
    }

    /// <summary>
    /// Whether this transition is about a personal-finance household.
    /// </summary>
    /// <remarks>
    /// Load-bearing, not defensive. Without it an Arke Kids <b>family</b> was written into
    /// <c>PersonalProfile.HouseholdId</c> and then vetoed a second family as "already belongs to a
    /// household" — this module imposing its exclusivity, its profile link and its events on every
    /// other product that uses the platform group service. That is precisely the coupling ADR-015
    /// exists to remove, reintroduced from the other side of the seam.
    ///
    /// An <b>empty</b> kind counts as a household. Every group written before Spec 086 has one — the
    /// migration defaults the column to "" and the backfill that fills it is disabled by default —
    /// and treating those as "not mine" would drop the profile link, the exclusivity rule and the
    /// legacy events for every household that already exists.
    /// </remarks>
    private static bool IsHousehold(GroupTransition transition)
        => string.IsNullOrEmpty(transition.GroupKind)
            || string.Equals(transition.GroupKind, GroupKinds.Household, StringComparison.OrdinalIgnoreCase);

    private static bool IsSelfRemoval(GroupTransition transition)
        => (transition.MemberPartyId is { } memberPartyId && memberPartyId == transition.ActorPartyId)
            || (transition.MemberUserId is { } memberUserId
                && transition.ActorUserId is { } actorUserId
                && memberUserId == actorUserId);

    private async Task LinkProfileAsync(Guid tenantId, Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        var profile = await FindProfileAsync(tenantId, userId, cancellationToken);
        if (profile is not null)
        {
            profile.HouseholdId = groupId;
        }
    }

    /// <summary>
    /// Accepting one invitation declines the rest.
    /// </summary>
    /// <remarks>
    /// Not tidiness: a user who accepts household A while still holding a pending invitation to B
    /// would otherwise be able to accept B too and end up with a profile pointing at one household
    /// and an accepted membership in another.
    /// </remarks>
    private async Task DeclineOtherPendingInvitationsAsync(
        Guid tenantId,
        Guid userId,
        Guid acceptedGroupId,
        CancellationToken cancellationToken)
    {
        var others = await _dbContext.HouseholdMembers
            .Where(member => member.TenantId == tenantId
                && member.UserId == userId
                && member.HouseholdId != acceptedGroupId
                && member.InvitationStatus == GroupMemberStatuses.Pending)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(other);
            other.InvitationStatus = GroupMemberStatuses.Declined;
            other.RespondedAt = _clock.UtcNow;
        }
    }

    private async Task UnlinkAsync(Guid tenantId, Guid userId, Guid groupId, CancellationToken cancellationToken)
    {
        var profile = await FindProfileAsync(tenantId, userId, cancellationToken);
        if (profile is not null && profile.HouseholdId == groupId)
        {
            profile.HouseholdId = null;
        }

        var ownedGroupAccounts = await _dbContext.PersonalAccounts
            .Where(account => account.TenantId == tenantId
                && account.UserId == userId
                && account.HouseholdId == groupId)
            .ToListAsync(cancellationToken);

        foreach (var account in ownedGroupAccounts)
        {
            // Leaving a household must take your accounts with you. Left shared, they would stay
            // visible to people you no longer share a household with.
            account.HouseholdId = null;
            _dbContext.EnqueueIntegrationEvent(new HouseholdAccountUnsharedEvent(tenantId, groupId, account.Id));
        }
    }

    private Task<PersonalProfile?> FindProfileAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
        => _dbContext.PersonalProfiles
            .FirstOrDefaultAsync(profile => profile.TenantId == tenantId && profile.UserId == userId, cancellationToken);
}
