using System.Data;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Contracts.Services.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Finance.Services.PersonalFinance;

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
    private readonly IEventBus _eventBus;
    private readonly IUserNotificationWriter _notificationWriter;

    public HouseholdService(
        PersonalFinanceDbContext dbContext,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IFinancialLifeGraphCacheInvalidator cacheInvalidator,
        IPartyReader partyReader,
        IUserDirectoryReader userDirectoryReader,
        IClock clock,
        IEventBus eventBus,
        IUserNotificationWriter notificationWriter)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _cacheInvalidator = cacheInvalidator;
        _partyReader = partyReader;
        _userDirectoryReader = userDirectoryReader;
        _clock = clock;
        _eventBus = eventBus;
        _notificationWriter = notificationWriter;
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
        var profile = await GetRequiredPersonalProfileAsync(userId, tenantId, cancellationToken);

        var memberships = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var membership in memberships)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);
            if (HouseholdMembershipRules.IsAccepted(membership))
            {
                throw new InvalidOperationException("User already belongs to a household.");
            }
        }

        if (profile.HouseholdId.HasValue)
        {
            throw new InvalidOperationException("User already belongs to a household.");
        }

        var household = new Household
        {
            TenantId = tenantId,
            Name = request.Name.Trim()
        };

        var member = new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = household.Id,
            UserId = userId,
            Role = HouseholdRoles.Owner,
            PermissionsJson = HouseholdMembershipRules.SerializePermissions(HouseholdMembershipRules.EmptyPermissions),
            InvitationStatus = HouseholdInvitationStatuses.Accepted,
            InvitedAt = _clock.UtcNow
        };

        _dbContext.Households.Add(household);
        _dbContext.HouseholdMembers.Add(member);

        profile.HouseholdId = household.Id;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new HouseholdCreatedEvent(tenantId, household.Id, userId), cancellationToken);
        await _cacheInvalidator.InvalidateUserGraphAsync(userId, cancellationToken);

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
        var normalizedRole = HouseholdMembershipRules.NormalizeRole(request.Role);
        var permissions = HouseholdMembershipRules.NormalizePermissions(request.Permissions);
        var household = await GetRequiredHouseholdAsync(request.HouseholdId, tenantId, cancellationToken);
        var inviterMembership = await GetRequiredAcceptedMembershipAsync(tenantId, household.Id, inviterUserId, cancellationToken);

        if (!HouseholdMembershipRules.CanManageMembers(inviterMembership))
        {
            throw new UnauthorizedAccessException("Only household owners or managers can invite members.");
        }

        await EnsureUserExistsWithPersonalProfileAsync(request.UserId, tenantId, cancellationToken);

        var acceptedMembershipElsewhere = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == request.UserId)
            .ToListAsync(cancellationToken);

        foreach (var membership in acceptedMembershipElsewhere)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(membership);
            if (membership.HouseholdId != household.Id && HouseholdMembershipRules.IsAccepted(membership))
            {
                throw new InvalidOperationException("User already belongs to a household.");
            }
        }

        var existingMembership = await _dbContext.HouseholdMembers
            .FirstOrDefaultAsync(
                member => member.TenantId == tenantId && member.HouseholdId == household.Id && member.UserId == request.UserId,
                cancellationToken);

        var now = _clock.UtcNow;

        if (existingMembership != null)
        {
            HouseholdMembershipRules.NormalizeLegacyMember(existingMembership);

            if (HouseholdMembershipRules.IsAccepted(existingMembership))
            {
                throw new InvalidOperationException("User is already a member of this household.");
            }

            if (HouseholdMembershipRules.IsPending(existingMembership) && !IsExpired(existingMembership, now))
            {
                throw new InvalidOperationException("Household invitation is already pending.");
            }

            existingMembership.Role = normalizedRole;
            existingMembership.PermissionsJson = HouseholdMembershipRules.SerializePermissions(permissions);
            existingMembership.InvitationStatus = HouseholdInvitationStatuses.Pending;
            existingMembership.InvitedByUserId = inviterUserId;
            existingMembership.InvitedAt = now;
            existingMembership.ExpiresAt = now.AddDays(InvitationExpiryDays);
            existingMembership.RespondedAt = null;

            await _dbContext.SaveChangesAsync(cancellationToken);

            var invitationResponse = await BuildInvitationResponseAsync(existingMembership, household.Name, inviterUserId, cancellationToken);
            await PublishInvitationSideEffectsAsync(tenantId, household, existingMembership, inviterUserId, cancellationToken);
            return invitationResponse;
        }

        var member = new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = household.Id,
            UserId = request.UserId,
            Role = normalizedRole,
            PermissionsJson = HouseholdMembershipRules.SerializePermissions(permissions),
            InvitationStatus = HouseholdInvitationStatuses.Pending,
            InvitedByUserId = inviterUserId,
            InvitedAt = now,
            ExpiresAt = now.AddDays(InvitationExpiryDays)
        };

        _dbContext.HouseholdMembers.Add(member);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var createdInvitation = await BuildInvitationResponseAsync(member, household.Name, inviterUserId, cancellationToken);
        await PublishInvitationSideEffectsAsync(tenantId, household, member, inviterUserId, cancellationToken);
        return createdInvitation;
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
        var now = _clock.UtcNow;
        var useTransaction = _dbContext.Database.IsRelational();
        var committed = false;
        await using var transaction = useTransaction
            ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            : null;

        try
        {
            var invitation = await _dbContext.HouseholdMembers
                .FirstOrDefaultAsync(
                    member => member.TenantId == tenantId
                        && member.HouseholdId == householdId
                        && member.UserId == userId,
                    cancellationToken)
                ?? throw new InvalidOperationException("Pending household invitation not found.");

            HouseholdMembershipRules.NormalizeLegacyMember(invitation);

            if (!HouseholdMembershipRules.IsPending(invitation))
            {
                throw new InvalidOperationException("Pending household invitation not found.");
            }

            if (IsExpired(invitation, now))
            {
                throw new InvalidOperationException("Household invitation has expired.");
            }

            var competingMemberships = await _dbContext.HouseholdMembers
                .AsNoTracking()
                .Where(member => member.TenantId == tenantId
                    && member.UserId == userId
                    && member.Id != invitation.Id)
                .ToListAsync(cancellationToken);

            foreach (var competingMembership in competingMemberships)
            {
                HouseholdMembershipRules.NormalizeLegacyMember(competingMembership);
                if (HouseholdMembershipRules.IsAccepted(competingMembership))
                {
                    throw new InvalidOperationException("User already belongs to a household.");
                }
            }

            invitation.Role = HouseholdMembershipRules.NormalizeRole(invitation.Role);
            invitation.InvitationStatus = HouseholdInvitationStatuses.Accepted;
            invitation.InvitedAt ??= invitation.CreatedAt;
            invitation.RespondedAt = now;

            var profile = await GetRequiredPersonalProfileAsync(userId, tenantId, cancellationToken);

            if (profile.HouseholdId.HasValue && profile.HouseholdId.Value != householdId)
            {
                throw new InvalidOperationException("User already belongs to a household.");
            }

            profile.HouseholdId = householdId;

            var otherPendingInvitations = await _dbContext.HouseholdMembers
                .Where(member => member.TenantId == tenantId
                    && member.UserId == userId
                    && member.Id != invitation.Id
                    && member.InvitationStatus == HouseholdInvitationStatuses.Pending)
                .ToListAsync(cancellationToken);

            foreach (var otherInvitation in otherPendingInvitations)
            {
                HouseholdMembershipRules.NormalizeLegacyMember(otherInvitation);
                otherInvitation.InvitationStatus = HouseholdInvitationStatuses.Declined;
                otherInvitation.RespondedAt = now;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }

            await _eventBus.PublishAsync(new HouseholdInvitationAcceptedEvent(tenantId, householdId, userId), cancellationToken);
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
                affectedAcceptedMembers.Select(item => item.UserId).Append(userId).Distinct(),
                cancellationToken);

            return MapMemberResponse(invitation);
        }
        catch
        {
            if (transaction != null && !committed)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            throw;
        }
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
        var invitation = await _dbContext.HouseholdMembers
            .FirstOrDefaultAsync(
                member => member.TenantId == tenantId
                    && member.HouseholdId == householdId
                    && member.UserId == userId,
                cancellationToken)
            ?? throw new InvalidOperationException("Pending household invitation not found.");

        HouseholdMembershipRules.NormalizeLegacyMember(invitation);

        if (!HouseholdMembershipRules.IsPending(invitation))
        {
            throw new InvalidOperationException("Pending household invitation not found.");
        }

        invitation.InvitationStatus = HouseholdInvitationStatuses.Declined;
        invitation.RespondedAt = _clock.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _eventBus.PublishAsync(new HouseholdInvitationDeclinedEvent(tenantId, householdId, userId), cancellationToken);
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
        var actorMembership = await GetRequiredAcceptedMembershipAsync(tenantId, householdId, actorUserId, cancellationToken);

        if (!HouseholdMembershipRules.CanManageMembers(actorMembership))
        {
            throw new UnauthorizedAccessException("Only household owners or managers can remove members.");
        }

        if (actorUserId == userId)
        {
            throw new InvalidOperationException("Use leave household to remove yourself.");
        }

        var targetMembership = await GetRequiredAcceptedMembershipAsync(tenantId, householdId, userId, cancellationToken);
        var acceptedMembers = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);

        if (HouseholdMembershipRules.IsOwner(targetMembership) && acceptedMembers.Count(HouseholdMembershipRules.IsOwner) == 1)
        {
            throw new InvalidOperationException("Cannot remove the sole accepted household owner.");
        }

        await RemoveMembershipAsync(targetMembership, actorUserId, tenantId, householdId, isSelfRemoval: false, cancellationToken);

        await _eventBus.PublishAsync(new HouseholdMemberRemovedEvent(tenantId, householdId, userId, actorUserId), cancellationToken);
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
        var membership = await GetRequiredAcceptedMembershipAsync(tenantId, householdId, userId, cancellationToken);
        var acceptedMembers = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);

        if (HouseholdMembershipRules.IsOwner(membership) && acceptedMembers.Count(HouseholdMembershipRules.IsOwner) == 1)
        {
            throw new InvalidOperationException("Transfer household ownership before leaving as the sole owner.");
        }

        await RemoveMembershipAsync(membership, userId, tenantId, householdId, isSelfRemoval: true, cancellationToken);

        await _eventBus.PublishAsync(new HouseholdMemberLeftEvent(tenantId, householdId, userId), cancellationToken);
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
        var currentUserId = GetCurrentUserId();
        var currentOwnerMembership = await GetRequiredAcceptedMembershipAsync(tenantId, householdId, currentUserId, cancellationToken);

        if (!HouseholdMembershipRules.IsOwner(currentOwnerMembership))
        {
            throw new UnauthorizedAccessException("Only a household owner can transfer ownership.");
        }

        var targetMembership = await GetRequiredAcceptedMembershipAsync(tenantId, householdId, newOwnerUserId, cancellationToken);
        currentOwnerMembership.Role = HouseholdRoles.Manager;
        targetMembership.Role = HouseholdRoles.Owner;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(
            new HouseholdOwnershipTransferredEvent(tenantId, householdId, currentUserId, newOwnerUserId),
            cancellationToken);

        var acceptedMembers = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);
        await _cacheInvalidator.InvalidateUserGraphsAsync(acceptedMembers.Select(item => item.UserId), cancellationToken);

        return (await GetMyHouseholdAsync(cancellationToken))
            ?? throw new InvalidOperationException("Household not found.");
    }

    public async Task<HouseholdDetailResponse?> GetMyHouseholdAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var userId = GetCurrentUserId();
        var memberships = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
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

        var invitations = await _dbContext.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.TenantId == tenantId
                && member.UserId == userId
                && member.InvitationStatus == HouseholdInvitationStatuses.Pending)
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
                invitation.UserId,
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
            member.UserId,
            cancellationToken);
        var inviterDisplayName = inviterDisplayNames.TryGetValue(inviterUserId, out var resolvedInviterDisplayName)
            ? resolvedInviterDisplayName
            : null;

        await _eventBus.PublishAsync(
            new HouseholdMemberInvitedEvent(tenantId, household.Id, member.UserId, inviterUserId, HouseholdMembershipRules.NormalizeRole(member.Role)),
            cancellationToken);

        await NotifyActorAsync(
            tenantId,
            member.UserId,
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

    private async Task RemoveMembershipAsync(
        HouseholdMember membership,
        Guid actorUserId,
        Guid tenantId,
        Guid householdId,
        bool isSelfRemoval,
        CancellationToken cancellationToken)
    {
        membership.InvitationStatus = HouseholdInvitationStatuses.Removed;
        membership.RespondedAt = _clock.UtcNow;

        var profile = await _dbContext.PersonalProfiles
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.UserId == membership.UserId, cancellationToken);

        if (profile != null && profile.HouseholdId == householdId)
        {
            profile.HouseholdId = null;
        }

        var ownedHouseholdAccounts = await _dbContext.PersonalAccounts
            .Where(account => account.TenantId == tenantId && account.UserId == membership.UserId && account.HouseholdId == householdId)
            .ToListAsync(cancellationToken);

        foreach (var account in ownedHouseholdAccounts)
        {
            account.HouseholdId = null;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var affectedUsers = await GetAcceptedMembershipsAsync(tenantId, householdId, cancellationToken);
        var affectedUserIds = affectedUsers.Select(item => item.UserId)
            .Append(membership.UserId)
            .Append(actorUserId)
            .Distinct()
            .ToList();

        await _cacheInvalidator.InvalidateUserGraphsAsync(affectedUserIds, cancellationToken);

        foreach (var account in ownedHouseholdAccounts)
        {
            await _eventBus.PublishAsync(new HouseholdAccountUnsharedEvent(tenantId, householdId, account.Id), cancellationToken);
        }

        if (!isSelfRemoval)
        {
            await NotifyActorAsync(
                tenantId,
                actorUserId,
                "household.member-removed",
                "Household member removed",
                "The household member was removed successfully.",
                "Info",
                "/household",
                JsonSerializer.Serialize(new { householdId, userId = membership.UserId }),
                cancellationToken);
        }
    }

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
            .Where(HouseholdMembershipRules.IsAccepted)
            .ToList();

        var displayNames = await ResolveDisplayNamesAsync(tenantId, acceptedMembers.Select(member => member.UserId), currentUserId, cancellationToken);
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
                member.UserId,
                displayNames.TryGetValue(member.UserId, out var displayName) ? displayName : $"Member {member.UserId}",
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
            member.UserId,
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

        return members.Where(HouseholdMembershipRules.IsAccepted).ToList();
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
            member.UserId,
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
