using System.Data;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Contracts.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.PersonalFinance.Services;

/// <summary>
/// PersonalFinance's household API, now a facade over <see cref="IGroupService"/> (Spec 086 P4).
/// </summary>
/// <remarks>
/// <para>
/// The lifecycle moved to <c>Aonik.Groups</c>; what stays here is everything the platform has no
/// business knowing — household vocabulary, the response shapes the mobile app and CLI already
/// consume, the user-facing notifications, and display names resolved through
/// <c>PersonalProfile → Party</c>. Every route, DTO and status code is unchanged, which is the whole
/// point of the facade surviving rather than the endpoints being re-pointed
/// (<a href="../../../../docs/specifications/086.extract-groups-and-sharing-to-platform.html">§11</a>).
/// </para>
/// <para>
/// Two translations happen here and are load-bearing. The facade keys on <b>user</b> and the group
/// service keys on <b>party</b>, so every call resolves one to the other; and the group service
/// throws SharedKernel exceptions while these endpoints have always mapped
/// <c>InvalidOperationException</c> to 409 and <c>UnauthorizedAccessException</c> to 403. Translating
/// here rather than changing the endpoints is what keeps the status codes identical.
/// </para>
/// </remarks>
internal sealed class HouseholdService : IHouseholdService
{
    private const string NotificationSource = "Finance.Household";
    private const int InvitationExpiryDays = 7;

    private readonly PersonalFinanceDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IFinancialLifeGraphCacheInvalidator _cacheInvalidator;
    private readonly IPartyReader _partyReader;
    private readonly IUserDirectoryReader _userDirectoryReader;
    private readonly IClock _clock;
    private readonly IUserNotificationWriter _notificationWriter;
    private readonly MemberPartyResolver _partyResolver;
    private readonly IGroupService _groupService;

    public HouseholdService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator,
        IPartyReader partyReader,
        IUserDirectoryReader userDirectoryReader,
        IClock clock,
        IUserNotificationWriter notificationWriter,
        MemberPartyResolver partyResolver,
        IGroupService groupService)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
        _partyReader = partyReader;
        _userDirectoryReader = userDirectoryReader;
        _clock = clock;
        _notificationWriter = notificationWriter;
        _partyResolver = partyResolver;
        _groupService = groupService;
    }

    public async Task<HouseholdResponse> CreateHouseholdAsync(
        CreateHouseholdRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Household name is required.", nameof(request.Name));
        }

        var userId = GetCurrentUserId();
        var tenantId = _tenantProvider.GetCurrentTenantId();

        // The profile checks, the profile link and HouseholdCreatedEvent all live in
        // PersonalFinanceGroupLifecycleContributor now, and run inside the group service's save.
        var group = await TranslateAsync(() =>
            _groupService.CreateAsync(new CreateGroupCommand(GroupKinds.Household, request.Name), cancellationToken));

        var member = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .FirstAsync(item => item.TenantId == tenantId && item.HouseholdId == group.Id && item.UserId == userId, cancellationToken);

        // Cache invalidation stays on this side of the seam, and after the commit rather than inside
        // it: invalidating while the transaction is still open lets a concurrent read repopulate the
        // cache from pre-commit state, which is worse than not invalidating at all.
        await _cacheInvalidator.InvalidateUserGraphAsync(userId, cancellationToken);

        var household = await GetRequiredHouseholdAsync(group.Id, tenantId, cancellationToken);

        return new HouseholdResponse(
            household.Id,
            household.Name,
            MapMemberResponse(member),
            household.CreatedAt);
    }

    public async Task<HouseholdInvitationResponse> InviteMemberAsync(
        InviteHouseholdMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.HouseholdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(request.HouseholdId));
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(request.UserId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var inviterUserId = GetCurrentUserId();
        var household = await GetRequiredHouseholdAsync(request.HouseholdId, tenantId, cancellationToken);

        // Existence of the invitee is checked by the contributor's veto, together with the
        // one-household rule — both are PersonalFinance facts about a household, and keeping them in
        // one place is what stops them drifting apart.
        var member = await TranslateAsync(() => _groupService.InviteAsync(
            new InviteGroupMemberCommand(household.Id, request.Role, UserId: request.UserId),
            cancellationToken));

        // Permissions are carried verbatim and deprecated (ShareGrant is what actually enforces
        // visibility), so they are written here rather than pushed onto a platform command.
        await ApplyLegacyPermissionsAsync(member.Id, request.Permissions, cancellationToken);

        var row = await _dbContext.HouseholdMembers.AsNoTracking()
            .FirstAsync(item => item.Id == member.Id, cancellationToken);

        var invitationResponse = await BuildInvitationResponseAsync(row, household.Name, inviterUserId, cancellationToken);
        await PublishInvitationSideEffectsAsync(tenantId, household, row, inviterUserId, cancellationToken);
        return invitationResponse;
    }

    /// <summary>
    /// Writes the deprecated <c>PermissionsJson</c> blob the household API still accepts.
    /// </summary>
    /// <remarks>
    /// Not pushed onto <c>InviteGroupMemberCommand</c>: permissions are a PersonalFinance concept
    /// that Spec 048 already superseded with <c>ShareGrant</c>, and putting them on a platform
    /// command would give every future product a field it should never set. Carried verbatim until
    /// the column goes.
    /// </remarks>
    private async Task ApplyLegacyPermissionsAsync(
        Guid membershipId,
        IReadOnlyList<string>? permissions,
        CancellationToken cancellationToken)
    {
        var normalized = HouseholdMembershipRules.NormalizePermissions(permissions);

        if (normalized.Count == 0)
        {
            return;
        }

        var member = await _dbContext.HouseholdMembers.FirstOrDefaultAsync(item => item.Id == membershipId, cancellationToken);

        if (member is null)
        {
            return;
        }

        member.PermissionsJson = HouseholdMembershipRules.SerializePermissions(normalized);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<HouseholdMemberResponse> AcceptInvitationAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(householdId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();

        var membershipId = await RequireMembershipIdAsync(tenantId, householdId, userId, "Pending household invitation not found.", cancellationToken);

        await TranslateAsync(() => _groupService.AcceptInvitationAsync(membershipId, cancellationToken));

        var accepted = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .FirstAsync(member => member.Id == membershipId, cancellationToken);

        await NotifyActorAsync(
            tenantId,
            userId,
            "household.accepted",
            "Household joined",
            "You joined the household successfully.",
            "Success",
            $"/household",
            JsonSerializer.Serialize(new { householdId }),
            cancellationToken);

        var affectedAcceptedMembers = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);
        await _cacheInvalidator.InvalidateUserGraphsAsync(
            affectedAcceptedMembers.Select(item => item.UserId!.Value).Append(userId).Distinct(),
            cancellationToken);

        return MapMemberResponse(accepted);
    }

    public async Task DeclineInvitationAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(householdId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var membershipId = await RequireMembershipIdAsync(tenantId, householdId, userId, "Pending household invitation not found.", cancellationToken);

        // The event moved with the transition: the contributor publishes it inside the group
        // service's save, so it can no longer describe a decline that did not commit.
        await TranslateAsync(() => _groupService.DeclineInvitationAsync(membershipId, cancellationToken));
    }

    public async Task RemoveMemberAsync(
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(householdId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var actorUserId = GetCurrentUserId();

        if (actorUserId == userId)
        {
            // Kept here rather than in the group service: leaving and being removed are the same
            // transition to a group, and only this module has a separate route for each.
            throw new InvalidOperationException("Use leave household to remove yourself.");
        }

        var membershipId = await RequireMembershipIdAsync(tenantId, householdId, userId, "Household membership not found.", cancellationToken);

        await TranslateAsync(() => _groupService.RemoveMemberAsync(membershipId, cancellationToken));
        await InvalidateAfterRemovalAsync(tenantId, householdId, userId, actorUserId, cancellationToken);

        await NotifyActorAsync(
            tenantId,
            userId,
            "household.removed",
            "Removed from household",
            "You were removed from a household.",
            "Warning",
            "/household",
            JsonSerializer.Serialize(new { householdId, removedByUserId = actorUserId }),
            cancellationToken);

        await NotifyActorAsync(
            tenantId,
            actorUserId,
            "household.member-removed",
            "Household member removed",
            "The household member was removed successfully.",
            "Info",
            "/household",
            JsonSerializer.Serialize(new { householdId, userId }),
            cancellationToken);
    }

    public async Task LeaveHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(householdId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var membershipId = await RequireMembershipIdAsync(tenantId, householdId, userId, "Household membership not found.", cancellationToken);

        await TranslateAsync(() => _groupService.RemoveMemberAsync(membershipId, cancellationToken));
        await InvalidateAfterRemovalAsync(tenantId, householdId, userId, userId, cancellationToken);

        await NotifyActorAsync(
            tenantId,
            userId,
            "household.left",
            "Left household",
            "You left the household.",
            "Info",
            "/household",
            JsonSerializer.Serialize(new { householdId }),
            cancellationToken);
    }

    public async Task<HouseholdDetailResponse> TransferOwnershipAsync(
        Guid householdId,
        Guid newOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        if (householdId == Guid.Empty)
        {
            throw new ArgumentException("HouseholdId is required.", nameof(householdId));
        }

        if (newOwnerUserId == Guid.Empty)
        {
            throw new ArgumentException("NewOwnerUserId is required.", nameof(newOwnerUserId));
        }

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var membershipId = await RequireMembershipIdAsync(tenantId, householdId, newOwnerUserId, "Household membership not found.", cancellationToken);

        await TranslateAsync(() => _groupService.TransferOwnershipAsync(householdId, membershipId, cancellationToken));

        var acceptedMembers = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);
        await _cacheInvalidator.InvalidateUserGraphsAsync(acceptedMembers.Select(item => item.UserId!.Value), cancellationToken);

        return (await GetMyHouseholdAsync(cancellationToken))
            ?? throw new InvalidOperationException("Household not found.");
    }

    public async Task<HouseholdDetailResponse?> GetMyHouseholdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        // Household-facing, so it must only see households — otherwise a parent in an Arke Kids
        // family gets that family returned as "my household", with its members and its name.
        var memberships = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .Join(
                _dbContext.Households.AsNoTracking().Where(group => group.TenantId == tenantId
                    && (group.Kind == GroupKinds.Household || group.Kind == "")),
                member => member.HouseholdId,
                group => group.Id,
                (member, _) => member)
            .OrderByDescending(member => member.CreatedAt)
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return null;
        }

        foreach (var membership in memberships)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);
        }

        var acceptedMembership = memberships.FirstOrDefault(HouseholdMembershipRules.IsAccepted);
        if (acceptedMembership == null)
        {
            return null;
        }

        return await BuildHouseholdDetailAsync(tenantId, acceptedMembership.HouseholdId, userId, cancellationToken);
    }

    public async Task<IReadOnlyList<HouseholdInvitationResponse>> GetPendingInvitationsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var now = _clock.UtcNow;

        // Same boundary: a pending invitation to a family is not a household invitation, and
        // listing it here would offer it for acceptance on a route that then treats it as one.
        var invitations = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId
                && member.UserId == userId
                && member.InvitationStatus == HouseholdInvitationStatuses.Pending)
            .Join(
                _dbContext.Households.AsNoTracking().Where(group => group.TenantId == tenantId
                    && (group.Kind == GroupKinds.Household || group.Kind == "")),
                member => member.HouseholdId,
                group => group.Id,
                (member, _) => member)
            .OrderByDescending(member => member.InvitedAt ?? member.CreatedAt)
            .ToListAsync(cancellationToken);

        if (invitations.Count == 0)
        {
            return [];
        }

        foreach (var invitation in invitations)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(invitation);
        }

        invitations = invitations
            .Where(invitation => !IsExpired(invitation, now))
            .ToList();

        if (invitations.Count == 0)
        {
            return [];
        }

        var householdNames = await _dbContext.Households
            .AsNoTracking()
            .Where(household => household.TenantId == tenantId && invitations.Select(member => member.HouseholdId).Contains(household.Id))
            .ToDictionaryAsync(household => household.Id, household => household.Name, cancellationToken);

        var displayNames = await ResolveDisplayNamesAsync(
            tenantId,
            invitations.Where(item => item.InvitedByUserId.HasValue).Select(item => item.InvitedByUserId!.Value),
            userId,
            cancellationToken);

        return invitations
            .Select(invitation => new HouseholdInvitationResponse(
                invitation.Id,
                invitation.HouseholdId,
                householdNames.TryGetValue(invitation.HouseholdId, out var householdName) ? householdName : "Household",
                invitation.UserId!.Value,
                HouseholdMembershipRules.NormalizeRole(invitation.Role),
                HouseholdMembershipRules.NormalizeInvitationStatus(invitation.InvitationStatus),
                invitation.InvitedByUserId,
                invitation.InvitedByUserId.HasValue && displayNames.TryGetValue(invitation.InvitedByUserId.Value, out var invitedByDisplayName)
                    ? invitedByDisplayName
                    : null,
                invitation.InvitedAt,
                invitation.RespondedAt,
                invitation.ExpiresAt,
                invitation.CreatedAt))
            .OrderByDescending(item => item.InvitedAt ?? item.CreatedAt)
            .ToList();
    }

    private async Task PublishInvitationSideEffectsAsync(
        Guid tenantId,
        Household household,
        HouseholdMember member,
        Guid inviterUserId,
        CancellationToken cancellationToken)
    {
        var inviterDisplayNames = await ResolveDisplayNamesAsync(
            tenantId,
            [inviterUserId],
            member.UserId!.Value,
            cancellationToken);
        var inviterDisplayName = inviterDisplayNames.TryGetValue(inviterUserId, out var resolvedInviterDisplayName)
            ? resolvedInviterDisplayName
            : null;

        await NotifyActorAsync(
            tenantId,
            member.UserId!.Value,
            "household.invited",
            $"Invitation to join {household.Name}",
            string.IsNullOrWhiteSpace(inviterDisplayName)
                ? $"You were invited to join {household.Name}."
                : $"{inviterDisplayName} invited you to join {household.Name}.",
            "Info",
            "/household/invitations",
            JsonSerializer.Serialize(new { householdId = household.Id, invitedByUserId = inviterUserId }),
            cancellationToken);
    }

    /// <summary>
    /// Invalidates every life graph a departure touches, once the group service has committed.
    /// </summary>
    /// <remarks>
    /// The profile unlink and account unshare that used to sit here moved into
    /// <see cref="PersonalFinanceGroupLifecycleContributor"/>, where they run inside the membership
    /// transaction. What is left is cache invalidation, which deliberately does <b>not</b> belong in
    /// there: invalidating before commit lets a concurrent read repopulate from pre-commit state.
    /// </remarks>
    private async Task InvalidateAfterRemovalAsync(
        Guid tenantId,
        Guid householdId,
        Guid memberUserId,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        var remaining = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);

        var affectedUserIds = remaining.Select(item => item.UserId!.Value)
            .Append(memberUserId)
            .Append(actorUserId)
            .Distinct()
            .ToList();

        await _cacheInvalidator.InvalidateUserGraphsAsync(affectedUserIds, cancellationToken);
    }

    /// <summary>
    /// Finds the membership id the group service works in, from the user this module works in.
    /// </summary>
    private async Task<Guid> RequireMembershipIdAsync(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        string notFoundMessage,
        CancellationToken cancellationToken)
    {
        // Joined to the group, and only households qualify. Every legacy mutation reaches the
        // generic service through here — accept, decline, remove, leave, transfer — so without the
        // kind check a family id passed to a household route would mutate the other product's group
        // while this facade emitted household notifications over the top of it. Empty counts as a
        // household: every group written before Spec 086 has one.
        var membershipId = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.HouseholdId == householdId && member.UserId == userId)
            .Join(
                _dbContext.Households.AsNoTracking().Where(group => group.TenantId == tenantId
                    && (group.Kind == GroupKinds.Household || group.Kind == "")),
                member => member.HouseholdId,
                group => group.Id,
                (member, _) => (Guid?)member.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return membershipId ?? throw new InvalidOperationException(notFoundMessage);
    }

    /// <summary>
    /// Runs a group-service call, restating its exceptions in the ones these endpoints map.
    /// </summary>
    /// <remarks>
    /// The endpoints have always turned <c>InvalidOperationException</c> into 409 and
    /// <c>UnauthorizedAccessException</c> into 403. <c>GroupService</c> throws SharedKernel
    /// exceptions instead, and letting those through would turn documented 409s into 500s — a
    /// silent break of exactly the wire compatibility this facade exists to preserve. Messages are
    /// carried verbatim, because the endpoints return them.
    /// </remarks>
    private static async Task<T> TranslateAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (PermissionDeniedException ex)
        {
            throw new UnauthorizedAccessException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is InvalidStateException or NotFoundException)
        {
            throw new InvalidOperationException(ex.Message, ex);
        }
    }

    private static async Task TranslateAsync(Func<Task> call)
        => await TranslateAsync<bool>(async () =>
        {
            await call();
            return true;
        });

    private async Task<HouseholdDetailResponse> BuildHouseholdDetailAsync(
        Guid tenantId,
        Guid householdId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var household = await GetRequiredHouseholdAsync(householdId, tenantId, cancellationToken);
        var members = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.HouseholdId == householdId)
            .OrderBy(member => member.CreatedAt)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(member);
        }

        var acceptedMembers = members
            .Where(HouseholdMembershipRules.IsAcceptedUserMember)
            .ToList();

        var displayNames = await ResolveDisplayNamesAsync(tenantId, acceptedMembers.Select(member => member.UserId!.Value), currentUserId, cancellationToken);
        var inviterIds = acceptedMembers.Where(member => member.InvitedByUserId.HasValue).Select(member => member.InvitedByUserId!.Value).Distinct().ToList();
        var inviterDisplayNames = inviterIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await ResolveDisplayNamesAsync(tenantId, inviterIds, currentUserId, cancellationToken);

        var currentMembership = acceptedMembers.FirstOrDefault(member => member.UserId == currentUserId)
            ?? throw new InvalidOperationException("Household membership not found.");

        var responseMembers = acceptedMembers
            .Select(member => new HouseholdMemberDetailResponse(
                member.Id,
                member.HouseholdId,
                member.UserId!.Value,
                displayNames.TryGetValue(member.UserId!.Value, out var displayName) ? displayName : $"Member {member.UserId}",
                HouseholdMembershipRules.NormalizeRole(member.Role),
                HouseholdMembershipRules.ParsePermissions(member.PermissionsJson),
                HouseholdMembershipRules.NormalizeInvitationStatus(member.InvitationStatus),
                member.UserId == currentUserId,
                member.InvitedByUserId,
                member.InvitedByUserId.HasValue && inviterDisplayNames.TryGetValue(member.InvitedByUserId.Value, out var invitedByDisplayName)
                    ? invitedByDisplayName
                    : null,
                member.InvitedAt,
                member.RespondedAt,
                member.ExpiresAt,
                member.CreatedAt))
            .ToList();

        return new HouseholdDetailResponse(
            household.Id,
            household.Name,
            HouseholdMembershipRules.NormalizeRole(currentMembership.Role),
            responseMembers,
            household.CreatedAt);
    }

    private async Task<HouseholdInvitationResponse> BuildInvitationResponseAsync(
        HouseholdMember member,
        string householdName,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var inviterDisplayNames = member.InvitedByUserId.HasValue
            ? await ResolveDisplayNamesAsync(_tenantProvider.GetCurrentTenantId(), [member.InvitedByUserId.Value], currentUserId, cancellationToken)
            : new Dictionary<Guid, string>();

        return new HouseholdInvitationResponse(
            member.Id,
            member.HouseholdId,
            householdName,
            member.UserId!.Value,
            HouseholdMembershipRules.NormalizeRole(member.Role),
            HouseholdMembershipRules.NormalizeInvitationStatus(member.InvitationStatus),
            member.InvitedByUserId,
            member.InvitedByUserId.HasValue && inviterDisplayNames.TryGetValue(member.InvitedByUserId.Value, out var invitedByDisplayName)
                ? invitedByDisplayName
                : null,
            member.InvitedAt,
            member.RespondedAt,
            member.ExpiresAt,
            member.CreatedAt);
    }

    private async Task<Dictionary<Guid, string>> ResolveDisplayNamesAsync(
        Guid tenantId,
        IEnumerable<Guid> userIds,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var distinctUserIds = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctUserIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var profiles = await _dbContext.PersonalProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && distinctUserIds.Contains(profile.UserId))
            .ToListAsync(cancellationToken);

        var partyIds = profiles.Select(profile => profile.PartyId).Distinct().ToList();
        var partyLookup = partyIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _partyReader.GetByIdsAsync(tenantId, partyIds, cancellationToken))
                .ToDictionary(party => party.PartyId, party => party.DisplayName);

        var users = await _userDirectoryReader.GetByIdsAsync(tenantId, distinctUserIds, cancellationToken);

        var result = new Dictionary<Guid, string>();
        var profileLookup = profiles.ToDictionary(profile => profile.UserId, profile => profile.PartyId);
        var userLookup = users.ToDictionary(user => user.UserId, user => user.Email);

        foreach (var userId in distinctUserIds)
        {
            if (userId == currentUserId)
            {
                result[userId] = "You";
                continue;
            }

            if (profileLookup.TryGetValue(userId, out var partyId)
                && partyLookup.TryGetValue(partyId, out var displayName)
                && !string.IsNullOrWhiteSpace(displayName))
            {
                result[userId] = displayName.Trim();
                continue;
            }

            if (userLookup.TryGetValue(userId, out var email) && !string.IsNullOrWhiteSpace(email))
            {
                result[userId] = email.Trim();
                continue;
            }

            result[userId] = $"Member {userId}";
        }

        return result;
    }

    private async Task<Household> GetRequiredHouseholdAsync(Guid householdId, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.Households
            .FirstOrDefaultAsync(household => household.Id == householdId && household.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Household not found.");
    }

    private async Task<PersonalProfile> GetRequiredPersonalProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        return await _dbContext.PersonalProfiles
            .FirstOrDefaultAsync(item => item.UserId == userId && item.TenantId == tenantId, cancellationToken)
            ?? throw new InvalidOperationException("Personal profile is required to manage household membership.");
    }

    private async Task EnsureUserExistsWithPersonalProfileAsync(Guid userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var users = await _userDirectoryReader.GetByIdsAsync(tenantId, [userId], cancellationToken);

        if (users.Count == 0)
        {
            throw new InvalidOperationException("User not found.");
        }

        _ = await GetRequiredPersonalProfileAsync(userId, tenantId, cancellationToken);
    }

    private async Task<HouseholdMember> GetRequiredAcceptedMembershipAsync(
        Guid tenantId,
        Guid householdId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await _dbContext.HouseholdMembers
            .FirstOrDefaultAsync(
                member => member.TenantId == tenantId && member.HouseholdId == householdId && member.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Household membership not found.");

        HouseholdMembershipRules.NormalizeLegacyMember(membership);

        if (!HouseholdMembershipRules.IsAccepted(membership))
        {
            throw new InvalidOperationException("Household membership not found.");
        }

        return membership;
    }

    private async Task<List<HouseholdMember>> GetAcceptedMembershipsAsync(Guid tenantId, Guid householdId, CancellationToken cancellationToken)
    {
        var members = await _dbContext.HouseholdMembers
            .Where(member => member.TenantId == tenantId && member.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        foreach (var member in members)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(member);
        }

        return members.Where(HouseholdMembershipRules.IsAcceptedUserMember).ToList();
    }

    private async Task NotifyActorAsync(
        Guid tenantId,
        Guid userId,
        string type,
        string title,
        string body,
        string severity,
        string actionUrl,
        string? metadataJson,
        CancellationToken cancellationToken)
    {
        try
        {
            await _notificationWriter.WriteForUserAsync(
                new UserNotificationWriteRequest(
                    tenantId,
                    userId,
                    type,
                    NotificationSource,
                    title,
                    body,
                    severity,
                    actionUrl,
                    null,
                    null,
                    metadataJson),
                cancellationToken);
        }
        catch
        {
            // Household flows should still complete if notification delivery fails.
        }
    }

    private Guid GetCurrentUserId()
    {
        if (!_currentUserProvider.TryGetCurrentUserId(out var userId))
        {
            throw new InvalidOperationException("Authenticated user is required.");
        }

        return userId;
    }

    private static HouseholdMemberResponse MapMemberResponse(HouseholdMember member)
    {
        HouseholdMembershipRules.NormalizeLegacyMember(member);

        return new HouseholdMemberResponse(
            member.Id,
            member.HouseholdId,
            member.UserId!.Value,
            HouseholdMembershipRules.NormalizeRole(member.Role),
            HouseholdMembershipRules.ParsePermissions(member.PermissionsJson),
            HouseholdMembershipRules.NormalizeInvitationStatus(member.InvitationStatus),
            member.InvitedByUserId,
            member.InvitedAt,
            member.RespondedAt,
            member.ExpiresAt,
            member.CreatedAt);
    }

    private static bool IsExpired(HouseholdMember member, DateTime now)
        => member.ExpiresAt.HasValue && member.ExpiresAt.Value <= now;
}
