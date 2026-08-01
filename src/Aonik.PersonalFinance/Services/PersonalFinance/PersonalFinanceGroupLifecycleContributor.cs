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
        if (transition.MemberUserId is not { } userId)
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

        var memberships = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);

            if (HouseholdMembershipRules.IsAccepted(membership) && membership.HouseholdId != joiningGroupId)
            {
                return "User already belongs to a household.";
            }
        }

        return null;
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

        var memberships = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);

            if (HouseholdMembershipRules.IsAccepted(membership) && membership.HouseholdId != groupId)
            {
                return "User already belongs to a household.";
            }
        }

        return null;
    }

    public async Task OnCommittedAsync(GroupTransition transition, CancellationToken cancellationToken = default)
    {
        if (transition.MemberUserId is not { } userId)
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
                    // The role is not on the transition, and adding it would put one module's
                    // vocabulary on a platform record. The event's contract is unchanged, so the
                    // stored value is read back instead.
                    await ReadRoleAsync(tenantId, transition.GroupId, userId, cancellationToken)));
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
                // a different audit story. The transition distinguishes them by who acted.
                if (transition.ActorPartyId == transition.MemberPartyId)
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

    private async Task<string> ReadRoleAsync(Guid tenantId, Guid groupId, Guid userId, CancellationToken cancellationToken)
    {
        var role = await _dbContext.HouseholdMembers
            .Where(member => member.TenantId == tenantId && member.HouseholdId == groupId && member.UserId == userId)
            .Select(member => member.Role)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(role) ? HouseholdRoles.Viewer : HouseholdMembershipRules.NormalizeRole(role);
    }

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
