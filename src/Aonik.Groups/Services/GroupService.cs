using Aonik.Groups.Persistence;
using Aonik.PersonalFinance.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Aonik.SharedKernel.Events.Integration;

using System.Data;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Groups.Services;

/// <summary>
/// Group membership as a platform capability (Spec 086 §7, ADR-015).
/// </summary>
/// <remarks>
/// <para>
/// This is the generic half of what <c>HouseholdService</c> used to be: create, invite, accept,
/// decline, change role, transfer ownership, remove — plus the two rules that are genuinely about
/// groups rather than about finance (a group keeps at least one owner; an invitation expires).
/// </para>
/// <para>
/// Everything domain-specific went the other way, into <see cref="IGroupLifecycleContributor"/>.
/// That includes one rule that reads as generic and is not: <b>one group per member</b>. A
/// personal-finance household is exclusive because a user's accounts and life graph hang off exactly
/// one; a child of separated parents belongs to two families. So exclusivity is a contributor's veto,
/// not a rule here — which is exactly the seam doing its job.
/// </para>
/// <para>
/// Members are keyed by <b>party</b> throughout. Through the Spec 086 transition a row may still
/// carry a null <c>PartyId</c> (the P3 backfill is what closes that), so every party-keyed query
/// below tolerates absence rather than assuming it away.
/// </para>
/// </remarks>
internal sealed class GroupService : IGroupService, IGroupReader
{
    private const int InvitationExpiryDays = 7;

    private readonly IGroupDataContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IUserPartyResolver _userPartyResolver;
    private readonly IPersonalFinancePartyResolver? _profilePartyFallback;
    private readonly IPartyReader _partyReader;
    private readonly IClock _clock;
    private readonly IReadOnlyList<IGroupLifecycleContributor> _contributors;

    public GroupService(
        IGroupDataContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IUserPartyResolver userPartyResolver,
        IPartyReader partyReader,
        IClock clock,
        IEnumerable<IGroupLifecycleContributor> contributors,
        IPersonalFinancePartyResolver? profilePartyFallback = null)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _userPartyResolver = userPartyResolver;
        _profilePartyFallback = profilePartyFallback;
        _partyReader = partyReader;
        _clock = clock;
        _contributors = contributors.ToList();
    }

    // ── Writes ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The caller, by both keys.
    /// </summary>
    /// <remarks>
    /// <b>Both</b>, deliberately. Spec 086 P3 backfills <c>PartyId</c> onto every membership, and it
    /// is disabled by default — so on any environment where it has not run, a membership row still
    /// has a null party. Authorising purely on party would make those rows unusable by the very
    /// person they belong to: their own accept, leave and manage calls would fail. Matching on party
    /// <em>or</em> user is what makes P4 a dual-read window rather than a hard cutover; the user half
    /// goes when the column does, in the spec that drops it.
    /// </remarks>
    private readonly record struct Caller(Guid PartyId, Guid? UserId)
    {
        public bool Matches(HouseholdMember member)
            => (member.PartyId is { } partyId && partyId == PartyId)
                || (member.UserId is { } userId && UserId is { } callerUserId && userId == callerUserId);
    }

    public async Task<GroupDto> CreateAsync(CreateGroupCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ArgumentException("Group name is required.", nameof(command));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        var group = new Household
        {
            TenantId = tenantId,
            Kind = string.IsNullOrWhiteSpace(command.Kind) ? GroupKinds.Household : command.Kind.Trim(),
            Name = command.Name.Trim()
        };

        var owner = new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = group.Id,
            PartyId = caller.PartyId,
            UserId = caller.UserId,
            Role = GroupRoles.Owner,
            PermissionsJson = GroupMembershipRules.SerializePermissions(GroupMembershipRules.EmptyPermissions),
            InvitationStatus = GroupMemberStatuses.Accepted,
            InvitedAt = _clock.UtcNow
        };

        var transition = new GroupTransition(
            GroupTransitionKinds.Created, group.Id, caller.PartyId, caller.UserId, caller.PartyId, caller.UserId,
            GroupRoles.Owner, group.Kind);
        await VetoOrThrowAsync(transition, cancellationToken);

        _dbContext.Groups.Add(group);
        _dbContext.GroupMembers.Add(owner);

        // Contributors react BEFORE the save, not after it. They write through the same context, so
        // "react then save once" is what makes the same-transaction promise in
        // IGroupLifecycleContributor true rather than aspirational.
        await ReactAsync(transition, cancellationToken);

        _dbContext.EnqueueIntegrationEvent(new GroupCreatedEvent(
            tenantId, group.Id, group.Kind, caller.PartyId, caller.UserId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new GroupDto(group.Id, group.Kind, group.Name, [GroupMembershipRules.ToDto(owner)]);
    }

    public async Task<GroupMemberDto> AddMemberAsync(
        Guid groupId,
        Guid partyId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);
        var normalizedRole = GroupMembershipRules.NormalizeRole(role);

        var group = await RequireGroupAsync(tenantId, groupId, cancellationToken);
        await RequireManagerAsync(tenantId, groupId, caller, cancellationToken);
        RequireNotOwnerRole(normalizedRole);

        if (partyId == Guid.Empty || !await _partyReader.ExistsAsync(tenantId, partyId, cancellationToken))
        {
            // Rejected, not silently accepted. An unknown id here would create a membership nobody
            // can ever be authorised as, and a foreign one would leak a group across tenants.
            throw new NotFoundException($"Party {partyId} was not found in this tenant.");
        }

        // The consent boundary. Direct addition exists for people who cannot consent; anyone with a
        // login can, and belongs on the invitation path where their answer is recorded. Without this
        // check the two paths stop partitioning and the second becomes a way around the first.
        // Both sources, not just the bridge. A seeded or demo persona is linked to a login only
        // through PersonalProfile.PartyId, so asking AnkUserParties alone answers "no user" for
        // someone who plainly has one — and direct addition would then put them in a group without
        // ever asking. Fails closed: any evidence of a login sends them to the invitation path.
        var linkedUserId = await ResolveUserForPartyAsync(tenantId, partyId, cancellationToken);
        if (linkedUserId is not null)
        {
            throw new InvalidStateException("This party has a user and must be invited rather than added.");
        }

        var existing = await _dbContext.GroupMembers
            .FirstOrDefaultAsync(member => member.TenantId == tenantId
                && member.HouseholdId == groupId
                && member.PartyId == partyId, cancellationToken);

        if (existing is not null && GroupMembershipRules.IsAccepted(existing))
        {
            throw new InvalidStateException("This party is already a member of the group.");
        }

        var transition = new GroupTransition(
            GroupTransitionKinds.MemberAdded, group.Id, partyId, null, caller.PartyId, caller.UserId, normalizedRole,
            group.Kind);
        await VetoOrThrowAsync(transition, cancellationToken);

        var member = existing ?? new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = group.Id,
            PartyId = partyId
        };

        member.UserId = null;
        member.Role = normalizedRole;
        member.PermissionsJson = GroupMembershipRules.SerializePermissions(GroupMembershipRules.EmptyPermissions);

        // Accepted immediately, and there is nothing to accept: a party with no login cannot answer
        // an invitation, which is the entire reason this method exists alongside InviteAsync.
        member.InvitationStatus = GroupMemberStatuses.Accepted;
        member.InvitedAt = _clock.UtcNow;
        member.RespondedAt = _clock.UtcNow;
        member.ExpiresAt = null;

        if (existing is null)
        {
            _dbContext.GroupMembers.Add(member);
        }

        await ReactAsync(transition, cancellationToken);

        _dbContext.EnqueueIntegrationEvent(new GroupMemberAddedEvent(
            tenantId, group.Id, partyId, null, normalizedRole, caller.PartyId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupMembershipRules.ToDto(member);
    }

    public async Task<GroupMemberDto> InviteAsync(InviteGroupMemberCommand command, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);
        var normalizedRole = GroupMembershipRules.NormalizeRole(command.Role);

        var group = await RequireGroupAsync(tenantId, command.GroupId, cancellationToken);
        await RequireManagerAsync(tenantId, group.Id, caller, cancellationToken);
        RequireNotOwnerRole(normalizedRole);

        if (command.PartyId is null && command.UserId is null)
        {
            throw new InvalidStateException("An invitation must name the party or user being invited.");
        }

        var inviteePartyId = command.PartyId
            ?? await ResolvePartyForUserAsync(tenantId, command.UserId!.Value, cancellationToken);

        // Resolved BOTH ways. Inviting by party alone is the documented follow-up to AddMemberAsync
        // refusing a party that has a login — so it is exactly the path where the user id matters
        // most, and leaving it null hides the accepted membership from every user-keyed reader and
        // skips PersonalFinance's profile link, exclusivity check and legacy events entirely.
        var inviteeUserId = command.UserId
            ?? (command.PartyId is { } addressedPartyId
                ? await ResolveUserForPartyAsync(tenantId, addressedPartyId, cancellationToken)
                : null);

        // And when BOTH are supplied they must be the same person. Taking the caller's word for it
        // would let a manager mint one membership pairing party A with user B: B accepts through the
        // user key, and party-keyed readers then treat A as an accepted member of a group A never
        // agreed to join.
        if (command.PartyId is { } statedPartyId && command.UserId is { } statedUserId)
        {
            var resolvedForUser = await ResolvePartyForUserAsync(tenantId, statedUserId, cancellationToken);

            // Must resolve, and must match. Letting an unresolvable pair through was the same hole
            // with an extra step: the membership is stored with the stated party, and once the user
            // later gains a link they accept through the user key — acceptance sees a non-null party
            // and leaves it alone, so the unrelated party stands as an accepted member having never
            // agreed to anything.
            if (resolvedForUser is null || resolvedForUser != statedPartyId)
            {
                throw new InvalidStateException("The party and user named on this invitation are not the same person.");
            }
        }

        var now = _clock.UtcNow;

        // Re-inviting is the same transition as inviting, so it reuses the row rather than minting a
        // second membership — two rows for one person is what the filtered unique index forbids, and
        // it would make "are they in this group?" ambiguous.
        var existing = await FindMemberAsync(tenantId, group.Id, inviteePartyId, inviteeUserId, cancellationToken);

        if (existing is not null)
        {
            if (GroupMembershipRules.IsAccepted(existing))
            {
                throw new InvalidStateException("This person is already a member of the group.");
            }

            if (GroupMembershipRules.IsPending(existing) && !GroupMembershipRules.IsExpired(existing, now))
            {
                throw new InvalidStateException("An invitation is already pending.");
            }
        }

        var transition = new GroupTransition(
            GroupTransitionKinds.MemberInvited, group.Id, inviteePartyId, inviteeUserId, caller.PartyId, caller.UserId,
            normalizedRole, group.Kind);
        await VetoOrThrowAsync(transition, cancellationToken);

        var member = existing ?? new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = group.Id
        };

        member.PartyId = inviteePartyId;
        member.UserId = inviteeUserId;
        member.Role = normalizedRole;
        member.PermissionsJson = GroupMembershipRules.SerializePermissions(GroupMembershipRules.EmptyPermissions);
        member.InvitationStatus = GroupMemberStatuses.Pending;
        member.InvitedByUserId = caller.UserId;
        member.InvitedAt = now;
        member.RespondedAt = null;
        member.ExpiresAt = now.AddDays(InvitationExpiryDays);

        if (existing is null)
        {
            _dbContext.GroupMembers.Add(member);
        }

        await ReactAsync(transition, cancellationToken);

        _dbContext.EnqueueIntegrationEvent(new GroupMemberInvitedEvent(
            tenantId, group.Id, inviteePartyId ?? Guid.Empty, inviteeUserId, normalizedRole, caller.PartyId));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupMembershipRules.ToDto(member);
    }

    /// <summary>Finds a membership by either identifier — the same dual key every read here uses.</summary>
    private async Task<HouseholdMember?> FindMemberAsync(
        Guid tenantId,
        Guid groupId,
        Guid? partyId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var members = await _dbContext.GroupMembers
            .Where(member => member.TenantId == tenantId && member.HouseholdId == groupId)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            GroupMembershipRules.NormalizeLegacy(member);
        }

        return members.FirstOrDefault(member =>
            (partyId is not null && member.PartyId == partyId)
            || (userId is not null && member.UserId == userId));
    }

    /// <summary>Party to user, through the bridge then the profile — the mirror of the caller lookup.</summary>
    private async Task<Guid?> ResolveUserForPartyAsync(Guid tenantId, Guid partyId, CancellationToken cancellationToken)
    {
        var userId = await _userPartyResolver.GetUserIdForPartyAsync(tenantId, partyId, cancellationToken);

        if (userId is null && _profilePartyFallback is not null)
        {
            userId = await _profilePartyFallback.GetUserIdForPartyAsync(tenantId, partyId, cancellationToken);
        }

        return userId;
    }

    private async Task<Guid?> ResolvePartyForUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var partyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);

        if (partyId is null && _profilePartyFallback is not null)
        {
            partyId = await _profilePartyFallback.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);
        }

        // Null is tolerated, not fatal: the invitee may have no party yet, the columns are
        // dual-written, and the backfill is what closes the gap. Refusing here would make an
        // invitation that works today start failing at the cutover.
        return partyId;
    }

    public async Task<GroupMemberDto> AcceptInvitationAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        HouseholdMember? accepted = null;

        // EVERYTHING inside the transaction, including the read. Checking pending-and-unexpired
        // outside it let two concurrent accepts of one invitation both observe "pending" — and
        // HouseholdMember carries no concurrency token, so the loser would go on to run the
        // contributors again and enqueue a second acceptance event off its stale entity.
        await InTransactionAsync(async ct =>
        {
            var membership = await RequireMembershipAsync(tenantId, membershipId, ct);

            if (!caller.Matches(membership))
            {
                // An invitation is accepted BY the invitee. Letting anyone else accept it would make
                // the consent the invitation exists to record worthless.
                throw new PermissionDeniedException("An invitation can only be accepted by its recipient.");
            }

            if (!GroupMembershipRules.IsPending(membership))
            {
                throw new NotFoundException("Pending invitation not found.");
            }

            var now = _clock.UtcNow;
            if (GroupMembershipRules.IsExpired(membership, now))
            {
                throw new InvalidStateException("The invitation has expired.");
            }

            var group = await RequireGroupAsync(tenantId, membership.HouseholdId, ct);

            // A membership written before the party backfill has none, and the accepting caller's is
            // right here. Leaving it null keeps the row invisible to every party-keyed reader and
            // publishes an acceptance carrying Guid.Empty.
            membership.PartyId ??= caller.PartyId;

            var transition = new GroupTransition(
                GroupTransitionKinds.InviteAccepted,
                membership.HouseholdId,
                membership.PartyId,
                membership.UserId ?? caller.UserId,
                caller.PartyId,
                caller.UserId,
                membership.Role,
                group.Kind);

            // The veto is a read — "does this member already belong to an exclusive group?" — and two
            // concurrent accepts of two DIFFERENT invitations would each read "no" and each commit.
            // Serializable is what makes the veto mean anything under concurrency.
            await VetoOrThrowAsync(transition, ct);

            membership.Role = GroupMembershipRules.NormalizeRole(membership.Role);
            membership.InvitationStatus = GroupMemberStatuses.Accepted;
            membership.InvitedAt ??= membership.CreatedAt;
            membership.RespondedAt = now;

            await ReactAsync(transition, ct);

            _dbContext.EnqueueIntegrationEvent(new GroupInvitationAcceptedEvent(
                tenantId,
                membership.HouseholdId,
                membership.PartyId ?? Guid.Empty,
                membership.UserId));

            await _dbContext.SaveChangesAsync(ct);

            accepted = membership;
        }, cancellationToken);

        return GroupMembershipRules.ToDto(accepted!);
    }

    public async Task<GroupMemberDto> DeclineInvitationAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);
        var membership = await RequireMembershipAsync(tenantId, membershipId, cancellationToken);

        if (!caller.Matches(membership))
        {
            throw new PermissionDeniedException("An invitation can only be declined by its recipient.");
        }

        if (!GroupMembershipRules.IsPending(membership))
        {
            throw new NotFoundException("Pending invitation not found.");
        }

        membership.InvitationStatus = GroupMemberStatuses.Declined;
        membership.RespondedAt = _clock.UtcNow;

        var group = await RequireGroupAsync(tenantId, membership.HouseholdId, cancellationToken);

        var transition = new GroupTransition(
            GroupTransitionKinds.InviteDeclined, membership.HouseholdId, membership.PartyId, membership.UserId,
            caller.PartyId, caller.UserId, membership.Role, group.Kind);

        // No veto: refusing an invitation is the invitee's own decision, and a module that could
        // block it would be able to conscript someone into a group.
        await ReactAsync(transition, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupMembershipRules.ToDto(membership);
    }

    public async Task<GroupMemberDto> ChangeRoleAsync(Guid membershipId, string role, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);
        var normalizedRole = GroupMembershipRules.NormalizeRole(role);
        var membership = await RequireMembershipAsync(tenantId, membershipId, cancellationToken);

        await RequireManagerAsync(tenantId, membership.HouseholdId, caller, cancellationToken);

        // Ownership is granted by TRANSFER, never by role change. Without this a manager — who
        // passes the authorisation above — can call this on their own membership and promote
        // themselves, which is the whole point of TransferOwnershipAsync requiring an existing owner.
        if (string.Equals(normalizedRole, GroupRoles.Owner, StringComparison.Ordinal)
            && !GroupMembershipRules.IsOwner(membership))
        {
            throw new InvalidStateException("Ownership is granted by transferring it, not by changing a role.");
        }

        if (GroupMembershipRules.IsOwner(membership) && !string.Equals(normalizedRole, GroupRoles.Owner, StringComparison.Ordinal))
        {
            await RequireAnotherOwnerAsync(tenantId, membership, "Transfer ownership before demoting the sole owner.", cancellationToken);
        }

        membership.Role = normalizedRole;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return GroupMembershipRules.ToDto(membership);
    }

    public async Task<GroupDto> TransferOwnershipAsync(Guid groupId, Guid toMembershipId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);
        var group = await RequireGroupAsync(tenantId, groupId, cancellationToken);

        var currentOwner = await RequireAcceptedCallerMemberAsync(tenantId, groupId, caller, cancellationToken);
        if (!GroupMembershipRules.IsOwner(currentOwner))
        {
            throw new PermissionDeniedException("Only an owner can transfer ownership.");
        }

        var target = await RequireMembershipAsync(tenantId, toMembershipId, cancellationToken);
        if (target.HouseholdId != groupId || !GroupMembershipRules.IsAccepted(target))
        {
            throw new NotFoundException("Group membership not found.");
        }

        var transition = new GroupTransition(
            GroupTransitionKinds.OwnershipTransferred, groupId, target.PartyId, target.UserId, caller.PartyId, caller.UserId,
            GroupRoles.Owner, group.Kind);
        await VetoOrThrowAsync(transition, cancellationToken);

        // Ownership is a role, so the transfer is two role writes and no third field to fall out of
        // step with them.
        currentOwner.Role = GroupRoles.Manager;
        target.Role = GroupRoles.Owner;

        await ReactAsync(transition, cancellationToken);

        _dbContext.EnqueueIntegrationEvent(new GroupOwnershipTransferredEvent(
            tenantId, groupId, caller.PartyId, target.PartyId ?? Guid.Empty));

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildGroupDtoAsync(tenantId, group, cancellationToken);
    }

    public async Task RemoveMemberAsync(Guid membershipId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);
        var membership = await RequireMembershipAsync(tenantId, membershipId, cancellationToken);

        var isSelfRemoval = caller.Matches(membership);
        if (!isSelfRemoval)
        {
            await RequireManagerAsync(tenantId, membership.HouseholdId, caller, cancellationToken);
        }

        if (!GroupMembershipRules.IsAccepted(membership))
        {
            throw new NotFoundException("Group membership not found.");
        }

        if (GroupMembershipRules.IsOwner(membership))
        {
            await RequireAnotherOwnerAsync(
                tenantId,
                membership,
                isSelfRemoval
                    ? "Transfer ownership before leaving as the sole owner."
                    : "Cannot remove the sole accepted owner.",
                cancellationToken);
        }

        var group = await RequireGroupAsync(tenantId, membership.HouseholdId, cancellationToken);

        var transition = new GroupTransition(
            GroupTransitionKinds.MemberRemoved, membership.HouseholdId, membership.PartyId, membership.UserId,
            caller.PartyId, caller.UserId, membership.Role, group.Kind);
        await VetoOrThrowAsync(transition, cancellationToken);

        membership.InvitationStatus = GroupMemberStatuses.Removed;
        membership.RespondedAt = _clock.UtcNow;

        // The contributor clears the profile link and unshares owned accounts here. One save covers
        // both that and the status flip, so a crash cannot leave a member removed but still linked.
        await ReactAsync(transition, cancellationToken);

        _dbContext.EnqueueIntegrationEvent(new GroupMemberRemovedEvent(
            tenantId, membership.HouseholdId, membership.PartyId ?? Guid.Empty, membership.UserId, caller.PartyId));

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // ── Reads ───────────────────────────────────────────────────────────────────────────────

    public async Task<GroupDto?> GetAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var group = await _dbContext.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == groupId, cancellationToken);

        return group is null ? null : await BuildGroupDtoAsync(tenantId, group, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var caller = await RequireCallerAsync(tenantId, cancellationToken);

        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId
                && member.InvitationStatus == GroupMemberStatuses.Accepted
                && (member.PartyId == caller.PartyId
                    || (caller.UserId != null && member.UserId == caller.UserId)))
            .Select(member => member.HouseholdId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await LoadGroupsAsync(tenantId, groupIds, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupDto>> GetForPartyAsync(Guid partyId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId
                && member.InvitationStatus == GroupMemberStatuses.Accepted
                && member.PartyId == partyId)
            .Select(member => member.HouseholdId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await LoadGroupsAsync(tenantId, groupIds, cancellationToken);
    }

    private async Task<IReadOnlyList<GroupDto>> LoadGroupsAsync(
        Guid tenantId,
        List<Guid> groupIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count == 0)
        {
            return [];
        }

        var groups = await _dbContext.Groups
            .AsNoTracking()
            .Where(group => group.TenantId == tenantId && groupIds.Contains(group.Id))
            .OrderBy(group => group.CreatedAt)
            .ToListAsync(cancellationToken);

        var result = new List<GroupDto>(groups.Count);
        foreach (var group in groups)
        {
            result.Add(await BuildGroupDtoAsync(tenantId, group, cancellationToken));
        }

        return result;
    }

    public async Task<IReadOnlyList<GroupDto>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        var groupIds = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId
                && member.InvitationStatus == GroupMemberStatuses.Accepted
                && member.UserId == userId)
            .Select(member => member.HouseholdId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await LoadGroupsAsync(tenantId, groupIds, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _dbContext.Groups
            .AsNoTracking()
            .AnyAsync(group => group.TenantId == tenantId && group.Id == groupId, cancellationToken);
    }

    public async Task<IReadOnlyList<GroupMemberDto>> GetMembersAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var members = await LoadMembersAsync(tenantId, groupId, cancellationToken);

        // Accepted only: someone invited and still deciding is not in the group yet, and a reader
        // that showed them would make the group look bigger than the people who agreed to it.
        return members.Where(GroupMembershipRules.IsAccepted).Select(GroupMembershipRules.ToDto).ToList();
    }

    /// <summary>
    /// Runs <paramref name="work"/> under a serializable transaction, through the provider's
    /// execution strategy so a retry re-runs the whole unit rather than half of it.
    /// </summary>
    /// <remarks>
    /// Skipped entirely on a non-relational provider. The InMemory suite therefore proves nothing
    /// about this path — <c>UserTransactionsUnderRetryStrategySqlServerTests</c> is where it is real.
    /// </remarks>
    private async Task InTransactionAsync(Func<CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async ct =>
        {
            if (!_dbContext.Database.IsRelational())
            {
                await work(ct);
                return;
            }

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            try
            {
                await work(ct);
                await transaction.CommitAsync(ct);
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        }, cancellationToken);
    }

    // ── Contributor plumbing ────────────────────────────────────────────────────────────────

    private async Task VetoOrThrowAsync(GroupTransition transition, CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors)
        {
            var reason = await contributor.VetoAsync(transition, cancellationToken);
            if (!string.IsNullOrWhiteSpace(reason))
            {
                // The contributor's own words, not a generic refusal. "User already belongs to a
                // household" is what the endpoint has always returned, and wire compatibility
                // depends on it surviving the move.
                throw new InvalidStateException(reason);
            }
        }
    }

    private async Task ReactAsync(GroupTransition transition, CancellationToken cancellationToken)
    {
        foreach (var contributor in _contributors)
        {
            await contributor.OnCommittedAsync(transition, cancellationToken);
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private async Task<Caller> RequireCallerAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new PermissionDeniedException("An authenticated user is required.");
        }

        var partyId = await _userPartyResolver.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);

        if (partyId is null && _profilePartyFallback is not null)
        {
            // Seeded and demo personas carry a PersonalProfile with a synthetic user and no
            // AnkUserParties row. Without this fallback every caller on a seeded environment would
            // be unable to create or manage a group at all — the same reason
            // ProjectUserBriefEndpoint carries it in the other direction. Optional by design: a
            // deployment without PersonalFinance simply has no fallback to consult.
            partyId = await _profilePartyFallback.GetPartyIdForUserAsync(tenantId, userId, cancellationToken);
        }

        if (partyId is null)
        {
            throw new InvalidStateException("The current user is not linked to a party.");
        }

        return new Caller(partyId.Value, userId);
    }

    /// <summary>
    /// Ownership is conferred by transfer, never by naming a role.
    /// </summary>
    /// <remarks>
    /// Applies to <b>creation</b> as well as role change. Blocking self-promotion alone left the
    /// same escalation through another door: a manager could simply add a party-only owner, or
    /// invite an account straight in as owner, and never touch <c>TransferOwnershipAsync</c> — which
    /// is the one path that requires an existing owner.
    /// </remarks>
    private static void RequireNotOwnerRole(string normalizedRole)
    {
        if (string.Equals(normalizedRole, GroupRoles.Owner, StringComparison.Ordinal))
        {
            throw new InvalidStateException("Ownership is granted by transferring it, not by naming the role.");
        }
    }

    private async Task<Household> RequireGroupAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken)
        => await _dbContext.Groups
            .FirstOrDefaultAsync(group => group.TenantId == tenantId && group.Id == groupId, cancellationToken)
            ?? throw new NotFoundException($"Group {groupId} was not found.");

    private async Task<HouseholdMember> RequireMembershipAsync(Guid tenantId, Guid membershipId, CancellationToken cancellationToken)
    {
        var membership = await _dbContext.GroupMembers
            .FirstOrDefaultAsync(member => member.TenantId == tenantId && member.Id == membershipId, cancellationToken)
            ?? throw new NotFoundException("Group membership not found.");

        GroupMembershipRules.NormalizeLegacy(membership);
        return membership;
    }

    private async Task<HouseholdMember> RequireAcceptedCallerMemberAsync(
        Guid tenantId,
        Guid groupId,
        Caller caller,
        CancellationToken cancellationToken)
    {
        var members = await _dbContext.GroupMembers
            .Where(member => member.TenantId == tenantId && member.HouseholdId == groupId)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            GroupMembershipRules.NormalizeLegacy(member);
        }

        var membership = members.FirstOrDefault(caller.Matches)
            ?? throw new NotFoundException("Group membership not found.");

        if (!GroupMembershipRules.IsAccepted(membership))
        {
            throw new NotFoundException("Group membership not found.");
        }

        return membership;
    }

    private async Task RequireManagerAsync(Guid tenantId, Guid groupId, Caller caller, CancellationToken cancellationToken)
    {
        var actor = await RequireAcceptedCallerMemberAsync(tenantId, groupId, caller, cancellationToken);

        if (!GroupMembershipRules.CanManageMembers(actor))
        {
            throw new PermissionDeniedException("Only group owners or managers can change membership.");
        }
    }

    /// <summary>
    /// Refuses a change that would leave the group with no owner.
    /// </summary>
    /// <remarks>
    /// An ownerless group is unrecoverable through the API — nobody left can invite, remove or
    /// transfer — so this is the one structural rule the service keeps for itself rather than
    /// delegating to a contributor.
    /// </remarks>
    private async Task RequireAnotherOwnerAsync(
        Guid tenantId,
        HouseholdMember membership,
        string message,
        CancellationToken cancellationToken)
    {
        var members = await LoadMembersAsync(tenantId, membership.HouseholdId, cancellationToken);

        var otherOwners = members.Count(member =>
            member.Id != membership.Id && GroupMembershipRules.IsOwner(member));

        if (otherOwners == 0)
        {
            throw new InvalidStateException(message);
        }
    }

    private async Task<List<HouseholdMember>> LoadMembersAsync(Guid tenantId, Guid groupId, CancellationToken cancellationToken)
    {
        var members = await _dbContext.GroupMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.HouseholdId == groupId)
            .OrderBy(member => member.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            GroupMembershipRules.NormalizeLegacy(member);
        }

        return members;
    }

    private async Task<GroupDto> BuildGroupDtoAsync(Guid tenantId, Household group, CancellationToken cancellationToken)
    {
        var members = await LoadMembersAsync(tenantId, group.Id, cancellationToken);

        return new GroupDto(
            group.Id,
            string.IsNullOrWhiteSpace(group.Kind) ? GroupKinds.Household : group.Kind,
            group.Name,
            members.Where(GroupMembershipRules.IsAccepted).Select(GroupMembershipRules.ToDto).ToList());
    }
}
