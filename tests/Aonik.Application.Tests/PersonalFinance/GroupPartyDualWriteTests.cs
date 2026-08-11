using Aonik.Groups.Services;
using System;
using System.Linq;
using System.Threading.Tasks;
using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 086 P3 — the writer half of the transition.
///
/// The backfill only ever closes the gap for rows that already exist. Without dual-writing, every
/// grant, invite and membership created between the P2 migration and the P5 cutover would be born
/// with a null party column, and the backfill would have to be re-run forever to chase them. These
/// tests pin the writers: party columns populated <b>alongside</b> the user ones, never instead of
/// them, and never at the cost of a flow that works today.
/// </summary>
public sealed class GroupPartyDualWriteTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private sealed class TestClock : Aonik.SharedKernel.Abstractions.IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private PersonalFinanceDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"DualWrite_{Guid.NewGuid()}").Options, new TestTenantProvider(_tenantId));

    /// <summary>The real resolver over a no-link bridge, so these exercise the PersonalProfile fallback.</summary>
    private MemberPartyResolver Resolver(PersonalFinanceDbContext ctx)
        => new(ctx, Mock.Of<IUserPartyResolver>());

    private CircleService Circle(PersonalFinanceDbContext ctx, Guid userId)
        => new(
            ctx,
            new TestTenantProvider(_tenantId),
            Mock.Of<Aonik.SharedKernel.Abstractions.Multitenancy.ITenantContext>(),
            new TestCurrentUserProvider(userId),
            Mock.Of<Aonik.SharedKernel.Abstractions.Documents.IDocumentLinkReader>(),
            Mock.Of<IPartyReader>(),
            Resolver(ctx),
            new ShareGrantService(
                ctx,
                new TestTenantProvider(_tenantId),
                Mock.Of<Aonik.SharedKernel.Abstractions.Multitenancy.ITenantContext>(),
                new TestCurrentUserProvider(userId),
                Mock.Of<IUserPartyResolver>(),
                Mock.Of<IPartyReader>(),
                [new CareEntityShareResourceResolver(ctx, new TestTenantProvider(_tenantId), Mock.Of<IUserPartyResolver>(), new PersonalFinancePartyResolver(ctx))],
                new TestClock(),
                new PersonalFinancePartyResolver(ctx)),
            Microsoft.Extensions.Options.Options.Create(new CircleInviteOptions()));

    private async Task<Guid> SeedProfileAsync(PersonalFinanceDbContext ctx, Guid userId)
    {
        var partyId = Guid.NewGuid();
        ctx.PersonalProfiles.Add(new PersonalProfile
        {
            Id = Guid.NewGuid(), TenantId = _tenantId, UserId = userId, PartyId = partyId
        });
        await ctx.SaveChangesAsync();
        return partyId;
    }

    [Fact]
    public async Task CreateGrant_Should_PopulateBothPartyIds_AlongsideTheUserIds()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var ownerParty = await SeedProfileAsync(ctx, owner);
        var memberParty = await SeedProfileAsync(ctx, member);

        await Circle(ctx, owner).CreateGrantAsync(
            new CreateCircleGrantRequest(member, "docsOnly", Array.Empty<Guid>(), true));

        var grant = await ctx.CircleGrants.SingleAsync();

        grant.OwnerPartyId.Should().Be(ownerParty);
        grant.MemberPartyId.Should().Be(memberParty);

        // Alongside, not instead of. The deployed reader still compares these against the
        // authenticated user id — re-pointing them would make every grant vanish until P5.
        grant.OwnerUserId.Should().Be(owner);
        grant.MemberUserId.Should().Be(member);
    }

    [Fact]
    public async Task CreateGrant_Should_WriteResourceKindAndTerms()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        await SeedProfileAsync(ctx, owner);

        await Circle(ctx, owner).CreateGrantAsync(
            new CreateCircleGrantRequest(Guid.Empty, "docsOnly", Array.Empty<Guid>(), true));

        var grant = await ctx.CircleGrants.SingleAsync();

        grant.ResourceKind.Should().Be(ShareResourceKinds.CareEntity);

        // Byte-for-byte what the backfill writes. Two shapes would produce two corpora that only
        // disagree at the P5 cutover, by which point half the grants stop redacting.
        grant.TermsJson.Should().Be("{\"noAmounts\":true}");
        grant.NoAmounts.Should().BeTrue("the column is retained so a rollback needs no data recovery");
    }

    [Fact]
    public async Task CreateGrant_Should_Succeed_When_TheOwnerHasNoParty()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();

        // Sharing predates Spec 086 and works today for users with no party link at all. Dual-writing
        // must never turn a working flow into a failure, and neither must the P5 cutover — which is
        // why ownership is validated in whichever terms the owner actually has rather than in party
        // terms only. The backfill is what reports the gap, when an operator can act on it.
        var act = () => Circle(ctx, owner).CreateGrantAsync(
            new CreateCircleGrantRequest(Guid.Empty, "entities", Array.Empty<Guid>(), false));

        await act.Should().NotThrowAsync();
        (await ctx.CircleGrants.SingleAsync()).OwnerPartyId.Should().BeNull();
    }

    [Fact]
    public async Task ALegacyGrant_WithNoResourceKind_Should_StillBeVisible()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        await SeedProfileAsync(ctx, owner);
        await SeedProfileAsync(ctx, member);

        // Exactly what an upgrade produces: the migration defaults ResourceKind to "" and the
        // backfill that fills it is disabled by default.
        ctx.CircleGrants.Add(new CircleGrant
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            OwnerUserId = owner,
            MemberUserId = member,
            ResourceKind = string.Empty,
            Scope = "entities",
            EntityIdsJson = "[]",
            Status = "active"
        });
        await ctx.SaveChangesAsync();

        var shared = await Circle(ctx, member).ListGrantsForMemberAsync();

        // Filtering on the populated kind alone would make every pre-existing share vanish the
        // moment this deployed — silently revoking access nobody revoked.
        shared.Should().ContainSingle().Which.OwnerUserId.Should().Be(owner);
    }

    [Fact]
    public async Task CreateInvite_Should_PopulateOwnerPartyResourceKindAndTerms()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var ownerParty = await SeedProfileAsync(ctx, owner);

        await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", Array.Empty<Guid>(), false, null));

        var invite = await ctx.CircleInvites.SingleAsync();

        invite.OwnerPartyId.Should().Be(ownerParty);
        invite.ResourceKind.Should().Be(ShareResourceKinds.CareEntity);
        invite.TermsJson.Should().Be("{\"noAmounts\":false}");
    }

    [Fact]
    public async Task AcceptInvite_Should_CarryTheOwnerPartyFromTheInvite_AndResolveTheMember()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var ownerParty = await SeedProfileAsync(ctx, owner);
        var memberParty = await SeedProfileAsync(ctx, member);

        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("docsOnly", Array.Empty<Guid>(), true, null));

        await Circle(ctx, member).AcceptInviteAsync(invite.Token);

        var grant = await ctx.CircleGrants.SingleAsync();

        // The invite is the record of what was offered. Re-resolving the owner at acceptance could
        // pick up a link minted in between, quietly changing whose records were shared.
        grant.OwnerPartyId.Should().Be(ownerParty);
        grant.MemberPartyId.Should().Be(memberParty);
        grant.TermsJson.Should().Be("{\"noAmounts\":true}");
    }
}
