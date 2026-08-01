using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Groups.Services;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.SharedKernel.Abstractions.UserBrief;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 086 P4 — the extracted group lifecycle and the seam PersonalFinance hangs off it.
/// </summary>
/// <remarks>
/// <c>HouseholdServiceTests</c> already proves the household behaviour survived the move. What is
/// new here is the seam itself: that a contributor can refuse a transition, that its reaction lands
/// in the same save as the membership write, and that a member with no login passes straight through
/// a module that has nothing to say about them.
/// </remarks>
public sealed class GroupServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly TestClock _clock = new(new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc));
    private readonly Mock<IPartyReader> _partyReader = new();
    private readonly Mock<IUserPartyResolver> _userPartyResolver = new();

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; set; }
    }

    private PersonalFinanceDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"Groups_{Guid.NewGuid()}").Options, new TestTenantProvider(_tenantId));

    private GroupService CreateService(PersonalFinanceDbContext context, Guid callerUserId)
    {
        var tenantProvider = new TestTenantProvider(_tenantId);

        return new GroupService(
            context,
            tenantProvider,
            new TestCurrentUserProvider(callerUserId),
            _userPartyResolver.Object,
            _partyReader.Object,
            _clock,
            [new PersonalFinanceGroupLifecycleContributor(context, tenantProvider, _clock)],
            new PersonalFinancePartyResolver(context));
    }

    private async Task<Guid> SeedProfileAsync(PersonalFinanceDbContext context, Guid userId, Guid? householdId = null)
    {
        var partyId = Guid.NewGuid();
        context.PersonalProfiles.Add(new PersonalProfile
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = userId, PartyId = partyId, HouseholdId = householdId
        });
        await context.SaveChangesAsync();
        return partyId;
    }

    [Fact]
    public async Task CreateAsync_Should_LinkTheOwnersProfile_InTheSameSave()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(context, userId);

        var group = await CreateService(context, userId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        // The profile link is the contributor's work, written through the same DbContext instance
        // the membership was. If it needed a second save this assertion would still pass — but the
        // membership would be committable without it, which is the failure this design removes.
        var profile = await context.PersonalProfiles.SingleAsync(item => item.UserId == userId);
        profile.HouseholdId.Should().Be(group.Id);

        var member = await context.HouseholdMembers.SingleAsync();
        member.Role.Should().Be(GroupRoles.Owner);
        member.InvitationStatus.Should().Be(GroupMemberStatuses.Accepted);
    }

    [Fact]
    public async Task CreateAsync_Should_BeVetoed_When_TheCallerAlreadyBelongsToAHousehold()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(context, userId, householdId: Guid.NewGuid());

        var act = () => CreateService(context, userId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Second"));

        // One-household-per-user is a PersonalFinance rule, not a group rule — a child of separated
        // parents belongs to two families. This proves it survived the move as a veto rather than
        // being either lost or promoted into the platform.
        var thrown = await act.Should().ThrowAsync<InvalidStateException>();
        thrown.Which.Message.Should().Be("User already belongs to a household.");

        context.Households.Should().BeEmpty("a vetoed transition writes nothing");
    }

    [Fact]
    public async Task CreateAsync_Should_BeVetoed_When_TheCallerHasNoPersonalProfile()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();
        _userPartyResolver.Setup(r => r.GetPartyIdForUserAsync(_tenantId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(partyId);

        var act = () => CreateService(context, userId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        await act.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("Personal profile is required to manage household membership.");
    }

    [Fact]
    public async Task AddMemberAsync_Should_RejectAPartyThatHasAUser()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        var childPartyId = Guid.NewGuid();
        _partyReader.Setup(r => r.ExistsAsync(_tenantId, childPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userPartyResolver.Setup(r => r.GetUserIdForPartyAsync(_tenantId, childPartyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid());

        var act = () => CreateService(context, ownerUserId).AddMemberAsync(group.Id, childPartyId, GroupRoles.Viewer);

        // The consent boundary. Direct addition exists for people who cannot consent; if it also
        // accepted people who can, it would be a way to put any adult in a group without asking.
        await act.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*must be invited*");
    }

    [Fact]
    public async Task AddMemberAsync_Should_RejectAPartyFromAnotherTenant()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        var foreignPartyId = Guid.NewGuid();
        _partyReader.Setup(r => r.ExistsAsync(_tenantId, foreignPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var act = () => CreateService(context, ownerUserId).AddMemberAsync(group.Id, foreignPartyId, GroupRoles.Viewer);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task AddMemberAsync_Should_AddAPartyWithNoLogin_AndTheFinanceContributorShould_SayNothing()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        var childPartyId = Guid.NewGuid();
        _partyReader.Setup(r => r.ExistsAsync(_tenantId, childPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userPartyResolver.Setup(r => r.GetUserIdForPartyAsync(_tenantId, childPartyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var member = await CreateService(context, ownerUserId).AddMemberAsync(group.Id, childPartyId, GroupRoles.Viewer);

        member.PartyId.Should().Be(childPartyId);
        member.UserId.Should().BeNull();
        member.InvitationStatus.Should().Be(GroupMemberStatuses.Accepted, "a party with no login cannot answer an invitation");

        // The PersonalFinance contributor must skip a member it has nothing to say about. Failing
        // instead would make a children's product unusable on any deployment that includes finance.
        context.PersonalProfiles.Should().ContainSingle()
            .Which.UserId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task TwoPartyOnlyMembers_Should_BothJoinOneGroup()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        _partyReader.Setup(r => r.ExistsAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userPartyResolver.Setup(r => r.GetUserIdForPartyAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var service = CreateService(context, ownerUserId);
        await service.AddMemberAsync(group.Id, Guid.NewGuid(), GroupRoles.Viewer);
        await service.AddMemberAsync(group.Id, Guid.NewGuid(), GroupRoles.Viewer);

        // The whole point of the spec, end to end. The unique-index half of it is only observable on
        // SQL Server (GroupMemberUniqueIndexSqlServerTests); this is the service half.
        var members = await service.GetMembersAsync(group.Id);
        members.Should().HaveCount(3);
        members.Count(m => m.UserId is null).Should().Be(2);
    }

    [Fact]
    public async Task RemoveMemberAsync_Should_RefuseToLeaveTheGroupWithoutAnOwner()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var service = CreateService(context, ownerUserId);
        var group = await service.CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        var act = () => service.RemoveMemberAsync(group.Members.Single().Id);

        // The one structural rule GroupService keeps for itself: an ownerless group cannot be
        // recovered through the API, because nobody left can invite, remove or transfer.
        await act.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("*sole owner*");
    }

    [Fact]
    public async Task AddMemberAsync_Should_RejectACallerWhoIsNotAManager()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var outsiderUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        await SeedProfileAsync(context, outsiderUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        var act = () => CreateService(context, outsiderUserId).AddMemberAsync(group.Id, Guid.NewGuid(), GroupRoles.Viewer);

        // Authorised against the CALLER's standing in the group, never against the party being
        // added — a party is added by someone with the right, never by itself.
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
