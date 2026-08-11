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
    public async Task AUserBackedChange_Should_EmitBothTheGenericAndTheLegacyEvent()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(context, userId);

        await CreateService(context, userId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        // Both, deliberately: the generic event for consumers that understand parties, the legacy one
        // for subscribers that already exist. The duplication is the price of not breaking a live
        // subscriber, and it ends when the legacy set is retired.
        EventTypes(context).Should().Contain(name => name.Contains("GroupCreatedEvent"));
        EventTypes(context).Should().Contain(name => name.Contains("HouseholdCreatedEvent"));
    }

    [Fact]
    public async Task APartyOnlyChange_Should_EmitOnlyTheGenericEvent()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        var childPartyId = Guid.NewGuid();
        _partyReader.Setup(r => r.ExistsAsync(_tenantId, childPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userPartyResolver.Setup(r => r.GetUserIdForPartyAsync(_tenantId, childPartyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        await CreateService(context, ownerUserId).AddMemberAsync(group.Id, childPartyId, GroupRoles.Viewer);

        // The legacy payloads all carry a UserId, so a child has no legal one to publish. This is the
        // whole reason the party-based set is additive rather than a rename.
        EventTypes(context).Should().Contain(name => name.Contains("GroupMemberAddedEvent"));
        EventTypes(context).Should().NotContain(name => name.Contains("HouseholdMemberInvitedEvent"));
    }

    [Fact]
    public async Task InviteAsync_Should_BeVetoed_When_TheInviteeBelongsElsewhere()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var elsewhereId = Guid.NewGuid();
        await SeedProfileAsync(context, inviteeUserId, householdId: elsewhereId);

        // The group row too, not just the membership: the exclusivity scan joins to it now, because
        // counting a FAMILY as a household is exactly what let this module's rule reach into
        // another product's groups. A membership without its group cannot exist in the schema.
        context.Households.Add(new Household
        {
            Id = elsewhereId,
            TenantId = _tenantId,
            Kind = GroupKinds.Household,
            Name = "Elsewhere"
        });

        // An accepted membership, not just the profile link: the invite check has always scanned
        // memberships, and asserting against a profile field the old code never read would test a
        // rule this phase did not move.
        context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HouseholdId = elsewhereId,
            UserId = inviteeUserId,
            Role = GroupRoles.Owner,
            PermissionsJson = "[]",
            InvitationStatus = GroupMemberStatuses.Accepted
        });
        await context.SaveChangesAsync();

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        var act = () => CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Viewer, UserId: inviteeUserId));

        // Inviting someone who already belongs elsewhere would mint an invitation they could never
        // accept. The rule is PersonalFinance's, so it lives in the contributor's veto — and the
        // message is the one the endpoint has always returned.
        await act.Should().ThrowAsync<InvalidStateException>()
            .WithMessage("User already belongs to a household.");
    }

    [Fact]
    public async Task InviteAsync_Should_RejectAnInvitationThatNamesNobody()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        var act = () => CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Viewer));

        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*name the party or user*");
    }

    [Fact]
    public async Task AddMemberAsync_Should_RejectAPartyLinkedToAUserOnlyThroughItsProfile()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        // A seeded persona: a profile carrying a party, and no AnkUserParties row.
        var adultUserId = Guid.NewGuid();
        var adultPartyId = await SeedProfileAsync(context, adultUserId);
        _partyReader.Setup(r => r.ExistsAsync(_tenantId, adultPartyId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _userPartyResolver.Setup(r => r.GetUserIdForPartyAsync(_tenantId, adultPartyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);

        var act = () => CreateService(context, ownerUserId).AddMemberAsync(group.Id, adultPartyId, GroupRoles.Viewer);

        // Asking only the bridge answers "no user" for someone who plainly has one, and direct
        // addition would then put an adult in a group without ever asking them. The consent boundary
        // has to fail closed on any evidence of a login, not just the authoritative kind.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*must be invited*");
    }

    [Fact]
    public async Task GetMineAsync_Should_NotReturnAGroupTheCallerHasLeft()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var leaverUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var leaverPartyId = await SeedProfileAsync(context, leaverUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        context.HouseholdMembers.Add(new HouseholdMember
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HouseholdId = group.Id,
            UserId = leaverUserId,
            PartyId = leaverPartyId,
            Role = GroupRoles.Viewer,
            PermissionsJson = "[]",
            InvitationStatus = GroupMemberStatuses.Removed
        });
        await context.SaveChangesAsync();

        var mine = await CreateService(context, leaverUserId).GetMineAsync();

        // A removed member is not a member. A status-blind lookup would hand them the group's name
        // and every accepted member in it, after their access should have ended.
        mine.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMineAsync_Should_NotReturnAGroupTheCallerHasOnlyBeenInvitedTo()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        await SeedProfileAsync(context, inviteeUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));
        await CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Viewer, UserId: inviteeUserId));

        var mine = await CreateService(context, inviteeUserId).GetMineAsync();

        mine.Should().BeEmpty("an invitation is an offer, not membership");
    }

    [Fact]
    public async Task InviteAsync_Should_PublishTheInvitedRole_NotADefault()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        await SeedProfileAsync(context, inviteeUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));
        await CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Manager, UserId: inviteeUserId));

        // The contributor runs BEFORE the save, so reading the role back from the table would miss
        // the uncommitted membership entirely and publish Viewer. It travels on the transition.
        var payload = context.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>()
            .Single(message => message.EventType.Contains("HouseholdMemberInvitedEvent"))
            .Payload;

        payload.Should().Contain(GroupRoles.Manager);
    }

    [Fact]
    public async Task AFamily_Should_NotBeTreatedAsAHousehold_ByTheFinanceContributor()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(context, userId);

        var family = await CreateService(context, userId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        // The contributor must ignore a group that is not its own kind. Writing a family into
        // PersonalProfile.HouseholdId would impose finance exclusivity on the other product, which
        // is the coupling ADR-015 exists to remove — reintroduced from the other side of the seam.
        var profile = await context.PersonalProfiles.SingleAsync(item => item.UserId == userId);
        profile.HouseholdId.Should().BeNull();

        EventTypes(context).Should().NotContain(name => name.Contains("HouseholdCreatedEvent"));
        EventTypes(context).Should().Contain(name => name.Contains("GroupCreatedEvent"));
    }

    [Fact]
    public async Task TwoFamilies_Should_BothBeJoinable_ByOneUser()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(context, userId);

        var service = CreateService(context, userId);
        await service.CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        // One household per user is a finance rule. A parent belongs to two families, and the
        // contributor vetoing the second would make the whole model unusable.
        var act = () => service.CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Okonkwos"));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AManager_Should_NotBeAbleToPromoteThemselvesToOwner()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        await SeedProfileAsync(context, managerUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));
        var invited = await CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Manager, UserId: managerUserId));
        await CreateService(context, managerUserId).AcceptInvitationAsync(invited.Id);

        var act = () => CreateService(context, managerUserId).ChangeRoleAsync(invited.Id, GroupRoles.Owner);

        // A manager passes the manage-members check, so without an explicit rule they could hand
        // themselves ownership — bypassing TransferOwnershipAsync, which requires an existing owner.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*transferring it*");
    }

    [Fact]
    public async Task InviteAsync_ByPartyAlone_Should_StillRecordTheUser()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var inviteePartyId = await SeedProfileAsync(context, inviteeUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        var member = await CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Viewer, PartyId: inviteePartyId));

        // Inviting by party alone is the documented follow-up to AddMemberAsync refusing a party
        // that has a login, so it is exactly where the user id matters most: leaving it null hides
        // the membership from every user-keyed reader and skips the finance contributor entirely.
        member.UserId.Should().Be(inviteeUserId);
    }

    [Fact]
    public async Task AParentInAFamily_Should_StillBeAbleToCreateAHousehold()
    {
        await using var context = CreateContext();
        var userId = Guid.NewGuid();
        await SeedProfileAsync(context, userId);

        var service = CreateService(context, userId);
        await service.CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));

        // The exclusivity scan counts households, not memberships. Scanning by user alone made a
        // family block this module's own first household — the same coupling as the other direction.
        var act = () => service.CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AcceptingAFamilyInvitation_Should_NotWriteTheFinanceProfileLink()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        await SeedProfileAsync(context, inviteeUserId);

        var family = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Family, "Keanes"));
        var invited = await CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(family.Id, GroupRoles.Viewer, UserId: inviteeUserId));

        await CreateService(context, inviteeUserId).AcceptInvitationAsync(invited.Id);

        // The creation path carried the kind; accepting did not, so a family invitation still ran
        // the finance contributor and wrote the family into PersonalProfile.HouseholdId.
        var profile = await context.PersonalProfiles.SingleAsync(item => item.UserId == inviteeUserId);
        profile.HouseholdId.Should().BeNull();
    }

    [Fact]
    public async Task AManager_Should_NotBeAbleToInviteSomeoneStraightInAsOwner()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        var act = () => CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Owner, UserId: Guid.NewGuid()));

        // Blocking self-promotion alone left the same escalation through another door.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*transferring it*");
    }

    [Fact]
    public async Task InviteAsync_Should_RejectAPartyAndUserThatAreDifferentPeople()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);

        var otherUserId = Guid.NewGuid();
        await SeedProfileAsync(context, otherUserId);
        var unrelatedPartyId = Guid.NewGuid();

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));

        var act = () => CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Viewer, PartyId: unrelatedPartyId, UserId: otherUserId));

        // One membership pairing party A with user B: B accepts through the user key, and every
        // party-keyed reader then treats A as a member of a group A never agreed to join.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*not the same person*");
    }

    [Fact]
    public async Task AcceptingALegacyInvitation_Should_BackfillTheMembersParty()
    {
        await using var context = CreateContext();
        var ownerUserId = Guid.NewGuid();
        var inviteeUserId = Guid.NewGuid();
        await SeedProfileAsync(context, ownerUserId);
        var inviteePartyId = await SeedProfileAsync(context, inviteeUserId);

        var group = await CreateService(context, ownerUserId).CreateAsync(new CreateGroupCommand(GroupKinds.Household, "Keane"));
        var invited = await CreateService(context, ownerUserId).InviteAsync(
            new InviteGroupMemberCommand(group.Id, GroupRoles.Viewer, UserId: inviteeUserId));

        // An invitation written before the party backfill.
        var row = await context.HouseholdMembers.FirstAsync(item => item.Id == invited.Id);
        row.PartyId = null;
        await context.SaveChangesAsync();

        await CreateService(context, inviteeUserId).AcceptInvitationAsync(invited.Id);

        // The accepting caller's party is right there. Leaving it null keeps the accepted membership
        // invisible to every party-keyed reader until someone remembers to run a disabled job.
        (await context.HouseholdMembers.FirstAsync(item => item.Id == invited.Id))
            .PartyId.Should().Be(inviteePartyId);
    }

    private static List<string> EventTypes(Aonik.PersonalFinance.Persistence.PersonalFinanceDbContext context)
        => context.Set<Aonik.SharedKernel.Events.Outbox.OutboxMessage>().Select(message => message.EventType).ToList();

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
