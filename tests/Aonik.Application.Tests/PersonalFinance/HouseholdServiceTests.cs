using System.Text.Json;
using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Events.Outbox;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.PersonalFinance;

public class HouseholdServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId)
        {
            _userId = userId;
        }

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }

    private sealed class RecordingGraphCacheInvalidator : IFinancialLifeGraphCacheInvalidator
    {
        public List<Guid> InvalidatedUserIds { get; } = [];

        public int CurrentUserInvalidationCount { get; private set; }

        public int AllGraphsInvalidationCount { get; private set; }

        public void InvalidateCurrentUserGraph()
        {
            CurrentUserInvalidationCount++;
        }

        public Task InvalidateCurrentUserGraphAsync(CancellationToken cancellationToken = default)
        {
            CurrentUserInvalidationCount++;
            return Task.CompletedTask;
        }

        public Task InvalidateUserGraphAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            InvalidatedUserIds.Add(userId);
            return Task.CompletedTask;
        }

        public Task InvalidateUserGraphsAsync(IEnumerable<Guid> userIds, CancellationToken cancellationToken = default)
        {
            InvalidatedUserIds.AddRange(userIds);
            return Task.CompletedTask;
        }

        public Task InvalidateAllGraphCachesAsync(CancellationToken cancellationToken = default)
        {
            AllGraphsInvalidationCount++;
            return Task.CompletedTask;
        }
    }

    private static IReadOnlyList<TEvent> ReadOutboxEvents<TEvent>(PersonalFinanceDbContext context)
        where TEvent : class
    {
        var eventType = typeof(TEvent).FullName;
        return context.Set<OutboxMessage>()
            .Where(message => message.EventType == eventType)
            .OrderBy(message => message.CreatedAt)
            .AsEnumerable()
            .Select(message => JsonSerializer.Deserialize<TEvent>(message.Payload, OutboxSerialization.Options)!)
            .ToList();
    }

    private sealed class RecordingNotificationWriter : IUserNotificationWriter
    {
        public List<UserNotificationWriteRequest> Requests { get; } = [];

        public Task WriteForUserAsync(UserNotificationWriteRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class StubPartyReader : IPartyReader
    {
        public Dictionary<Guid, PartyHistoryItem> Parties { get; } = [];
        public Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> partyIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartyHistoryItem>>(partyIds.Where(Parties.ContainsKey).Select(id => Parties[id]).ToList());
        public Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(Guid tenantId, Guid partyId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartyRelationshipHistoryItem>>([]);
        public Task<bool> ExistsAsync(Guid tenantId, Guid partyId, CancellationToken ct = default) => Task.FromResult(Parties.ContainsKey(partyId));
        public Task<bool> HasActiveRelationshipBetweenAsync(Guid tenantId, Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
    }

    private sealed class StubUserDirectoryReader : IUserDirectoryReader
    {
        public Dictionary<Guid, UserDirectoryItem> Users { get; } = [];
        public Task<IReadOnlyList<UserDirectoryItem>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<UserDirectoryItem>>(userIds.Where(Users.ContainsKey).Select(id => Users[id]).ToList());
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static HouseholdService CreateService(
        PersonalFinanceDbContext context,
        Guid tenantId,
        Guid userId,
        TestClock clock,
        RecordingGraphCacheInvalidator cacheInvalidator,
        RecordingNotificationWriter notificationWriter,
        IPartyReader partyReader,
        IUserDirectoryReader userDirectoryReader)
    {
        return new HouseholdService(
            context,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            cacheInvalidator,
            partyReader,
            userDirectoryReader,
            clock,
            notificationWriter);
    }

    [Fact]
    public async Task InviteMemberAsync_Should_CreatePendingInvitation_WithoutAssigningInviteeHousehold_AndUseInviterDisplayNameInNotification()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        var cacheInvalidator = new RecordingGraphCacheInvalidator();
        var notificationWriter = new RecordingNotificationWriter();
        var partyReader = new StubPartyReader();
        var userDirectoryReader = new StubUserDirectoryReader();

        using var context = CreateDbContext(tenantId);

        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, ownerUserId, "owner@example.com", "Alice Owner");
        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, inviteeUserId, "invitee@example.com", "Bob Invitee");

        var household = new Household
        {
            TenantId = tenantId,
            Name = "Home"
        };

        context.Households.Add(household);
        context.HouseholdMembers.Add(new HouseholdMember
        {
            TenantId = tenantId,
            HouseholdId = household.Id,
            UserId = ownerUserId,
            Role = HouseholdRoles.Owner,
            PermissionsJson = "[]",
            InvitationStatus = HouseholdInvitationStatuses.Accepted,
            InvitedAt = clock.UtcNow
        });

        var ownerProfile = await context.PersonalProfiles.SingleAsync(item => item.UserId == ownerUserId);
        ownerProfile.HouseholdId = household.Id;

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, ownerUserId, clock, cacheInvalidator, notificationWriter, partyReader, userDirectoryReader);

        // Act
        var result = await service.InviteMemberAsync(
            new InviteHouseholdMemberRequest(
                household.Id,
                inviteeUserId,
                HouseholdRoles.Viewer,
                ["read"]));

        // Assert
        result.HouseholdId.Should().Be(household.Id);
        result.UserId.Should().Be(inviteeUserId);
        result.Role.Should().Be(HouseholdRoles.Viewer);
        result.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Pending);
        result.InvitedByUserId.Should().Be(ownerUserId);

        var invitation = await context.HouseholdMembers.SingleAsync(item => item.HouseholdId == household.Id && item.UserId == inviteeUserId);
        invitation.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Pending);
        invitation.InvitedByUserId.Should().Be(ownerUserId);
        invitation.RespondedAt.Should().BeNull();
        invitation.ExpiresAt.Should().Be(clock.UtcNow.AddDays(7));

        var inviteeProfile = await context.PersonalProfiles.SingleAsync(item => item.UserId == inviteeUserId);
        inviteeProfile.HouseholdId.Should().BeNull();

        notificationWriter.Requests.Should().ContainSingle(request =>
            request.UserId == inviteeUserId
            && request.Type == "household.invited"
            && request.Body.Contains("Alice Owner invited you to join Home."));

        var invitedEvent = ReadOutboxEvents<HouseholdMemberInvitedEvent>(context).Single();
        invitedEvent.HouseholdId.Should().Be(household.Id);
        invitedEvent.InvitedUserId.Should().Be(inviteeUserId);
        invitedEvent.InvitedByUserId.Should().Be(ownerUserId);
        invitedEvent.Role.Should().Be(HouseholdRoles.Viewer);

        cacheInvalidator.InvalidatedUserIds.Should().BeEmpty();
    }

    [Fact]
    public async Task AcceptInvitationAsync_Should_AssignHousehold_AndDeclineOtherPendingInvitations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var targetOwnerUserId = Guid.NewGuid();
        var otherOwnerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 2, 2, 9, 30, 0, DateTimeKind.Utc));
        var cacheInvalidator = new RecordingGraphCacheInvalidator();
        var notificationWriter = new RecordingNotificationWriter();
        var partyReader = new StubPartyReader();
        var userDirectoryReader = new StubUserDirectoryReader();

        using var context = CreateDbContext(tenantId);

        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, targetOwnerUserId, "target-owner@example.com", "Target Owner");
        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, otherOwnerUserId, "other-owner@example.com", "Other Owner");
        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, inviteeUserId, "invitee@example.com", "Invitee User");

        var targetHousehold = new Household
        {
            TenantId = tenantId,
            Name = "Target Household"
        };

        var otherHousehold = new Household
        {
            TenantId = tenantId,
            Name = "Other Household"
        };

        context.Households.AddRange(targetHousehold, otherHousehold);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = targetHousehold.Id,
                UserId = targetOwnerUserId,
                Role = HouseholdRoles.Owner,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Accepted,
                InvitedAt = clock.UtcNow.AddDays(-10),
                RespondedAt = clock.UtcNow.AddDays(-10)
            },
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = otherHousehold.Id,
                UserId = otherOwnerUserId,
                Role = HouseholdRoles.Owner,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Accepted,
                InvitedAt = clock.UtcNow.AddDays(-10),
                RespondedAt = clock.UtcNow.AddDays(-10)
            },
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = targetHousehold.Id,
                UserId = inviteeUserId,
                Role = HouseholdRoles.Viewer,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Pending,
                InvitedByUserId = targetOwnerUserId,
                InvitedAt = clock.UtcNow.AddDays(-1),
                ExpiresAt = clock.UtcNow.AddDays(6)
            },
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = otherHousehold.Id,
                UserId = inviteeUserId,
                Role = HouseholdRoles.Manager,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Pending,
                InvitedByUserId = otherOwnerUserId,
                InvitedAt = clock.UtcNow.AddDays(-2),
                ExpiresAt = clock.UtcNow.AddDays(5)
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, inviteeUserId, clock, cacheInvalidator, notificationWriter, partyReader, userDirectoryReader);

        // Act
        var result = await service.AcceptInvitationAsync(targetHousehold.Id);

        // Assert
        result.HouseholdId.Should().Be(targetHousehold.Id);
        result.UserId.Should().Be(inviteeUserId);
        result.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Accepted);

        var acceptedMembership = await context.HouseholdMembers.SingleAsync(item => item.HouseholdId == targetHousehold.Id && item.UserId == inviteeUserId);
        acceptedMembership.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Accepted);
        acceptedMembership.RespondedAt.Should().Be(clock.UtcNow);

        var declinedMembership = await context.HouseholdMembers.SingleAsync(item => item.HouseholdId == otherHousehold.Id && item.UserId == inviteeUserId);
        declinedMembership.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Declined);
        declinedMembership.RespondedAt.Should().Be(clock.UtcNow);

        var inviteeProfile = await context.PersonalProfiles.SingleAsync(item => item.UserId == inviteeUserId);
        inviteeProfile.HouseholdId.Should().Be(targetHousehold.Id);

        notificationWriter.Requests.Should().ContainSingle(request =>
            request.UserId == inviteeUserId
            && request.Type == "household.accepted");

        var acceptedEvent = ReadOutboxEvents<HouseholdInvitationAcceptedEvent>(context).Single();
        acceptedEvent.HouseholdId.Should().Be(targetHousehold.Id);
        acceptedEvent.UserId.Should().Be(inviteeUserId);

        cacheInvalidator.InvalidatedUserIds.Distinct().Should().BeEquivalentTo([targetOwnerUserId, inviteeUserId]);
    }

    [Fact]
    public async Task RemoveMemberAsync_Should_ClearProfileAndUnshareOwnedAccounts()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 3, 10, 18, 45, 0, DateTimeKind.Utc));
        var cacheInvalidator = new RecordingGraphCacheInvalidator();
        var notificationWriter = new RecordingNotificationWriter();
        var partyReader = new StubPartyReader();
        var userDirectoryReader = new StubUserDirectoryReader();

        using var context = CreateDbContext(tenantId);

        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, ownerUserId, "owner@example.com", "Owner User");
        await SeedUserAsync(context, partyReader, userDirectoryReader, tenantId, memberUserId, "member@example.com", "Member User");

        var household = new Household
        {
            TenantId = tenantId,
            Name = "Family Household"
        };

        var sharedAccount = new PersonalAccount
        {
            TenantId = tenantId,
            UserId = memberUserId,
            HouseholdId = household.Id,
            Name = "Shared Spending",
            AccountType = "Current",
            Currency = "USD",
            Status = "Active"
        };

        context.Households.Add(household);
        context.PersonalAccounts.Add(sharedAccount);
        context.HouseholdMembers.AddRange(
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = household.Id,
                UserId = ownerUserId,
                Role = HouseholdRoles.Owner,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Accepted,
                InvitedAt = clock.UtcNow.AddDays(-7),
                RespondedAt = clock.UtcNow.AddDays(-7)
            },
            new HouseholdMember
            {
                TenantId = tenantId,
                HouseholdId = household.Id,
                UserId = memberUserId,
                Role = HouseholdRoles.Manager,
                PermissionsJson = "[]",
                InvitationStatus = HouseholdInvitationStatuses.Accepted,
                InvitedByUserId = ownerUserId,
                InvitedAt = clock.UtcNow.AddDays(-6),
                RespondedAt = clock.UtcNow.AddDays(-6)
            });

        var ownerProfile = await context.PersonalProfiles.SingleAsync(item => item.UserId == ownerUserId);
        ownerProfile.HouseholdId = household.Id;

        var memberProfile = await context.PersonalProfiles.SingleAsync(item => item.UserId == memberUserId);
        memberProfile.HouseholdId = household.Id;

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, ownerUserId, clock, cacheInvalidator, notificationWriter, partyReader, userDirectoryReader);

        // Act
        await service.RemoveMemberAsync(household.Id, memberUserId);

        // Assert
        var membership = await context.HouseholdMembers.SingleAsync(item => item.HouseholdId == household.Id && item.UserId == memberUserId);
        membership.InvitationStatus.Should().Be(HouseholdInvitationStatuses.Removed);
        membership.RespondedAt.Should().Be(clock.UtcNow);

        memberProfile.HouseholdId.Should().BeNull();
        sharedAccount.HouseholdId.Should().BeNull();

        notificationWriter.Requests.Should().Contain(request => request.UserId == memberUserId && request.Type == "household.removed");
        notificationWriter.Requests.Should().Contain(request => request.UserId == ownerUserId && request.Type == "household.member-removed");

        var removedEvent = ReadOutboxEvents<HouseholdMemberRemovedEvent>(context).Single();
        removedEvent.HouseholdId.Should().Be(household.Id);
        removedEvent.UserId.Should().Be(memberUserId);
        removedEvent.RemovedByUserId.Should().Be(ownerUserId);

        var unsharedEvent = ReadOutboxEvents<HouseholdAccountUnsharedEvent>(context).Single();
        unsharedEvent.HouseholdId.Should().Be(household.Id);
        unsharedEvent.AccountId.Should().Be(sharedAccount.Id);

        cacheInvalidator.InvalidatedUserIds.Distinct().Should().BeEquivalentTo([ownerUserId, memberUserId]);
    }

    private static async Task SeedUserAsync(
        PersonalFinanceDbContext context,
        StubPartyReader partyReader,
        StubUserDirectoryReader userDirectoryReader,
        Guid tenantId,
        Guid userId,
        string email,
        string displayName)
    {
        var partyId = Guid.NewGuid();

        userDirectoryReader.Users[userId] = new UserDirectoryItem(userId, email, "Active");
        partyReader.Parties[partyId] = new PartyHistoryItem(partyId, displayName, "Active", null);

        context.PersonalProfiles.Add(new PersonalProfile
        {
            TenantId = tenantId,
            UserId = userId,
            PartyId = partyId
        });

        await context.SaveChangesAsync();
    }
}
