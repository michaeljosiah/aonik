using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Infrastructure.Persistence;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.Platform.Entities.Identity;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using Aonik.Worker.Jobs;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 086 P3 — the backfill that populates what P2's columns left empty.
///
/// The phase ships on "zero unbackfilled rows in every environment, and no reader has changed", so
/// these tests are about two things: that the job resolves what it can, and that it <b>refuses</b>
/// rather than quietly leaving a live row without a party. A silent partial backfill is the failure
/// mode that matters — nothing observable breaks until the P5 reader cutover, by which point the
/// affected members and grants simply vanish.
/// </summary>
public sealed class GroupPartyBackfillJobTests
{
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly MutableTenantContext _tenantContext = new();

    /// <summary>
    /// The ambient tenant the job stamps as it works. Seeding uses it too, because the base context
    /// refuses to save a tenant-scoped row whose TenantId differs from the ambient tenant — which is
    /// the same enforcement that forces the job's per-tenant commit loop.
    /// </summary>
    private sealed class MutableTenantContext : ITenantContext, ITenantProvider
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;

        public Guid GetCurrentTenantId() => TenantId ?? Guid.Empty;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = TenantId ?? Guid.Empty;
            return TenantId.HasValue;
        }
    }

    private static IJobExecutionContext JobContext()
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return mock.Object;
    }

    private AonikDbContext CreateContext(string name)
        => new(
            new DbContextOptionsBuilder<AonikDbContext>().UseInMemoryDatabase(name).Options,
            _tenantContext);

    private GroupPartyBackfillJob CreateJob(AonikDbContext context)
        => new(context, _tenantContext, NullLogger<GroupPartyBackfillJob>.Instance);

    /// <summary>Commits seed rows under their own tenant, then clears the ambient tenant again.</summary>
    private async Task SaveSeedAsync(AonikDbContext context, Guid? tenantId = null)
    {
        _tenantContext.TenantId = tenantId ?? _tenantId;
        try
        {
            await context.SaveChangesAsync();
        }
        finally
        {
            _tenantContext.TenantId = null;
        }
    }

    private Guid SeedGroup(AonikDbContext context, string kind = "")
    {
        var group = new Household { Id = Guid.NewGuid(), TenantId = _tenantId, Kind = kind, Name = "Keane" };
        context.Households.Add(group);
        return group.Id;
    }

    private HouseholdMember SeedMember(AonikDbContext context, Guid groupId, Guid userId, bool deleted = false)
    {
        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            HouseholdId = groupId,
            UserId = userId,
            Role = HouseholdRoles.Viewer,
            PermissionsJson = "[]",
            InvitationStatus = HouseholdInvitationStatuses.Accepted,
            IsDeleted = deleted
        };
        context.HouseholdMembers.Add(member);
        return member;
    }

    private Guid SeedUserPartyLink(AonikDbContext context, Guid userId)
    {
        var partyId = Guid.NewGuid();
        context.UserParties.Add(new UserParty
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = userId,
            PartyId = partyId,
            LinkType = "owner"
        });
        return partyId;
    }

    private Guid SeedPersonalProfile(AonikDbContext context, Guid userId)
    {
        var partyId = Guid.NewGuid();
        context.PersonalProfiles.Add(new PersonalProfile
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = userId,
            PartyId = partyId
        });
        return partyId;
    }

    [Fact]
    public async Task Execute_Should_ResolveMemberParties_From_TheUserPartyBridge()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var userId = Guid.NewGuid();
        var expectedPartyId = SeedUserPartyLink(context, userId);
        var member = SeedMember(context, SeedGroup(context), userId);
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        member.PartyId.Should().Be(expectedPartyId);
    }

    [Fact]
    public async Task Execute_Should_FallBackToPersonalProfile_When_TheUserHasNoBridgeLink()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var userId = Guid.NewGuid();
        var profileParty = SeedPersonalProfile(context, userId);
        var member = SeedMember(context, SeedGroup(context), userId);
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        // Seeded and demo personas carry a profile with no bridge row. Without the fallback the job
        // would throw on every seeded environment, which teaches operators to ignore it.
        member.PartyId.Should().Be(profileParty);
    }

    [Fact]
    public async Task Execute_Should_PreferTheBridgeLink_Over_ThePersonalProfile()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var userId = Guid.NewGuid();
        SeedPersonalProfile(context, userId);
        var bridgeParty = SeedUserPartyLink(context, userId);
        var member = SeedMember(context, SeedGroup(context), userId);
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        // The bridge is authoritative and is what the platform itself reads from P5 onward. Resolving
        // to the profile instead would populate a party the cutover then disagrees with.
        member.PartyId.Should().Be(bridgeParty);
    }

    [Fact]
    public async Task Execute_Should_Throw_When_ALiveMemberResolvesToNoParty()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var orphanUserId = Guid.NewGuid();
        SeedMember(context, SeedGroup(context), orphanUserId);
        await SaveSeedAsync(context);

        var act = () => CreateJob(context).Execute(JobContext());

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain("resolve to no party");
    }

    [Fact]
    public async Task Execute_Should_NotThrow_When_TheOnlyUnresolvableRowIsSoftDeleted()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        SeedMember(context, SeedGroup(context), Guid.NewGuid(), deleted: true);
        await SaveSeedAsync(context);

        // A member deleted long ago whose user has since been purged is not a defect an operator can
        // fix. A backfill that can never go green is one whose failure everyone learns to ignore.
        var act = () => CreateJob(context).Execute(JobContext());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Execute_Should_LeaveAlreadyPopulatedRows_Untouched()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var userId = Guid.NewGuid();
        SeedUserPartyLink(context, userId);
        var member = SeedMember(context, SeedGroup(context), userId);
        var pinnedPartyId = Guid.NewGuid();
        member.PartyId = pinnedPartyId;
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        // Idempotence is what makes re-running the intended way to pick up rows written between
        // deployment and the first run — but only if a re-run cannot overwrite what P3's
        // dual-writers already put there.
        member.PartyId.Should().Be(pinnedPartyId);
    }

    [Fact]
    public async Task Execute_Should_BackfillGroupKind_To_Household()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var groupId = SeedGroup(context);
        var userId = Guid.NewGuid();
        SeedUserPartyLink(context, userId);
        SeedMember(context, groupId, userId);
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        var group = await context.Households.AcrossTenants().FirstAsync(item => item.Id == groupId);
        group.Kind.Should().Be(GroupKinds.Household);
    }

    [Fact]
    public async Task Execute_Should_BackfillGrant_ResourceKind_Terms_AndBothParties()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var ownerUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var ownerParty = SeedUserPartyLink(context, ownerUserId);
        var memberParty = SeedUserPartyLink(context, memberUserId);

        var grant = new CircleGrant
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OwnerUserId = ownerUserId,
            MemberUserId = memberUserId,
            Scope = "docsOnly",
            EntityIdsJson = "[]",
            NoAmounts = true,
            Status = "active"
        };
        context.CircleGrants.Add(grant);
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        grant.OwnerPartyId.Should().Be(ownerParty);
        grant.MemberPartyId.Should().Be(memberParty);
        grant.ResourceKind.Should().Be(ShareResourceKinds.CareEntity);

        // The redaction flag has to survive the move into opaque terms, in the exact shape the P3
        // dual-writer emits — a backfill that wrote a different casing would produce two corpora
        // that only disagree at the P5 cutover.
        grant.TermsJson.Should().Be("{\"noAmounts\":true}");

        // Retained, not replaced: a rollback between phases must need no data recovery.
        grant.NoAmounts.Should().BeTrue();
        grant.OwnerUserId.Should().Be(ownerUserId);
    }

    [Fact]
    public async Task Execute_Should_LeaveMemberPartyNull_On_APendingGrant()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var ownerUserId = Guid.NewGuid();
        SeedUserPartyLink(context, ownerUserId);

        var grant = new CircleGrant
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OwnerUserId = ownerUserId,
            MemberUserId = null,
            Scope = "entities",
            EntityIdsJson = "[]",
            Status = "pending"
        };
        context.CircleGrants.Add(grant);
        await SaveSeedAsync(context);

        // A pending grant has no member yet. That is not an unresolvable row, and treating it as one
        // would make the job fail on every outstanding invite.
        var act = () => CreateJob(context).Execute(JobContext());
        await act.Should().NotThrowAsync();

        grant.MemberPartyId.Should().BeNull();
        grant.OwnerPartyId.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_Should_BackfillInvites()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var ownerUserId = Guid.NewGuid();
        var ownerParty = SeedUserPartyLink(context, ownerUserId);

        var invite = new CircleInvite
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OwnerUserId = ownerUserId,
            Token = "t0ken",
            Scope = "entities",
            EntityIdsJson = "[]",
            NoAmounts = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Status = "pending"
        };
        context.CircleInvites.Add(invite);
        await SaveSeedAsync(context);

        await CreateJob(context).Execute(JobContext());

        invite.OwnerPartyId.Should().Be(ownerParty);
        invite.ResourceKind.Should().Be(ShareResourceKinds.CareEntity);
        invite.TermsJson.Should().Be("{\"noAmounts\":false}");
    }

    [Fact]
    public async Task Execute_Should_ReachAcrossTenants()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var otherTenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var partyId = Guid.NewGuid();

        context.UserParties.Add(new UserParty
        {
            Id = Guid.NewGuid(), TenantId = otherTenantId, UserId = userId, PartyId = partyId, LinkType = "owner"
        });
        var member = new HouseholdMember
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            HouseholdId = Guid.NewGuid(),
            UserId = userId,
            Role = HouseholdRoles.Owner,
            PermissionsJson = "[]",
            InvitationStatus = HouseholdInvitationStatuses.Accepted
        };
        context.HouseholdMembers.Add(member);
        await SaveSeedAsync(context, otherTenantId);

        // The job runs with no ambient tenant. A backfill scoped to one tenant would report success
        // while leaving every other tenant's rows to disappear at the cutover.
        await CreateJob(context).Execute(JobContext());

        member.PartyId.Should().Be(partyId);
    }

    [Fact]
    public async Task Execute_Should_NotResolveAcrossATenantBoundary()
    {
        await using var context = CreateContext($"Backfill_{Guid.NewGuid()}");
        var userId = Guid.NewGuid();

        // The same user id linked to a party in a DIFFERENT tenant.
        var foreignTenantId = Guid.NewGuid();
        context.UserParties.Add(new UserParty
        {
            Id = Guid.NewGuid(), TenantId = foreignTenantId, UserId = userId, PartyId = Guid.NewGuid(), LinkType = "owner"
        });
        await SaveSeedAsync(context, foreignTenantId);

        SeedMember(context, SeedGroup(context), userId);
        await SaveSeedAsync(context);

        // Resolving on user id alone would hand one tenant's member another tenant's party — the
        // worst possible outcome for a feature whose whole purpose is controlling who sees what.
        var act = () => CreateJob(context).Execute(JobContext());
        await act.Should().ThrowAsync<InvalidOperationException>();

        var member = await context.HouseholdMembers.AcrossTenants().FirstAsync(m => m.UserId == userId);
        member.PartyId.Should().BeNull();
    }
}
