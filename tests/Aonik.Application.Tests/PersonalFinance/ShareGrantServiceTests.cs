using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Groups.Services;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Persistence;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Groups;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 086 P5 — the extracted sharing mechanics.
/// </summary>
/// <remarks>
/// <c>CircleServiceTests</c> and <c>CircleEndpointsTests</c> already prove the circle behaviour
/// survived the split. What is pinned here is the platform's own safety: that ownership of the named
/// resources is always checked, and that a caller with no party cannot be mistaken for another
/// caller with no party.
/// </remarks>
public sealed class ShareGrantServiceTests
{
    private readonly Guid _tenantId = Guid.NewGuid();

    private sealed class Clock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
    }

    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private readonly Clock _clock = new();

    private PersonalFinanceDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"Share_{Guid.NewGuid()}").Options, new TestTenantProvider(_tenantId));

    private ShareGrantService Service(PersonalFinanceDbContext ctx, Guid userId)
    {
        var tenantProvider = new TestTenantProvider(_tenantId);
        var partyResolver = new PersonalFinancePartyResolver(ctx);

        return new ShareGrantService(
            ctx,
            tenantProvider,
            new MutableTenantContext { TenantId = _tenantId },
            new TestCurrentUserProvider(userId),
            // No AnkUserParties rows: these callers are party-less, which is what the deployed
            // Circle feature already permits and what P5 must not break.
            Mock.Of<IUserPartyResolver>(),
            Mock.Of<IPartyReader>(),
            [new CareEntityShareResourceResolver(ctx, tenantProvider, Mock.Of<IUserPartyResolver>(), partyResolver)],
            _clock,
            partyResolver);
    }

    private async Task<Guid> SeedEntityAsync(PersonalFinanceDbContext ctx, Guid ownerUserId, string name = "Mum")
    {
        var entity = new CareEntity
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = ownerUserId,
            Kind = "person",
            Name = name,
            CountryCode = "NG",
            AttributesJson = "{}"
        };
        ctx.CareEntities.Add(entity);
        await ctx.SaveChangesAsync();
        return entity.Id;
    }

    [Fact]
    public async Task CreateGrant_Should_Reject_AResourceKindWithNoResolver()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();

        var act = () => Service(ctx, owner).CreateGrantAsync(
            new CreateShareGrantCommand("entities", "invoice", [Guid.NewGuid()]));

        // Fails closed, like the agent approval gate does for an unclassified tool. An open string
        // with no registered owner is a typo sink, not an extension point.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*No resolver is registered*");
    }

    [Fact]
    public async Task CreateGrant_Should_Reject_ResourcesTheOwnerDoesNotOwn()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var theirEntityId = await SeedEntityAsync(ctx, someoneElse, "Their mum");

        var act = () => Service(ctx, owner).CreateGrantAsync(
            new CreateShareGrantCommand("entities", ShareResourceKinds.CareEntity, [theirEntityId]));

        // Checking only that a resolver EXISTS would let this through, and IShareGrantReader answers
        // authorisation from the stored ids alone — so it is a straightforward privilege escalation:
        // share what you do not own, then read it.
        await act.Should().ThrowAsync<InvalidStateException>().WithMessage("*only name resources its owner owns*");
        ctx.CircleGrants.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateGrant_Should_Accept_ResourcesTheOwnerOwns()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var grant = await Service(ctx, owner).CreateGrantAsync(
            new CreateShareGrantCommand("entities", ShareResourceKinds.CareEntity, [entityId], MemberUserId: Guid.NewGuid()));

        grant.ResourceIds.Should().ContainSingle().Which.Should().Be(entityId);
        grant.Status.Should().Be(ShareGrantStatuses.Active);
    }

    [Fact]
    public async Task AConsumedToken_Should_NotBeUsableByAnotherPartylessCaller()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var invite = await Service(ctx, owner).CreateInviteAsync(
            new CreateShareInviteCommand("entities", ShareResourceKinds.CareEntity, [entityId]));

        var accepted = await Service(ctx, member).AcceptInviteAsync(invite.Token);
        accepted.Status.Should().Be(ShareInviteAcceptStatus.Accepted);

        var replayed = await Service(ctx, stranger).AcceptInviteAsync(invite.Token);

        // The hazard this pins: none of these callers has a party, so a predicate written as
        // `grant.MemberPartyId == caller.PartyId` becomes `MemberPartyId IS NULL` in SQL — true for
        // every party-less member. Without the null guard the stranger is handed the member's grant.
        replayed.Status.Should().Be(ShareInviteAcceptStatus.Invalid);
        replayed.Grant.Should().BeNull();
    }

    [Fact]
    public async Task AReplayBySameMember_Should_ReturnTheSameGrant()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var invite = await Service(ctx, owner).CreateInviteAsync(
            new CreateShareInviteCommand("entities", ShareResourceKinds.CareEntity, [entityId]));

        var first = await Service(ctx, member).AcceptInviteAsync(invite.Token);
        var replay = await Service(ctx, member).AcceptInviteAsync(invite.Token);

        // Spec 049's parked-token flow replays accept on a cold start. Returning the held grant is
        // why the contract had to become a result rather than an exception.
        replay.Status.Should().Be(ShareInviteAcceptStatus.Accepted);
        replay.Grant!.Id.Should().Be(first.Grant!.Id);
        ctx.CircleGrants.Should().ContainSingle();
    }

    [Fact]
    public async Task RevokingAGrant_Should_KillTheInviteThatMintedIt()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var invite = await Service(ctx, owner).CreateInviteAsync(
            new CreateShareInviteCommand("entities", ShareResourceKinds.CareEntity, [entityId]));
        var accepted = await Service(ctx, member).AcceptInviteAsync(invite.Token);

        (await Service(ctx, owner).RevokeAsync(accepted.Grant!.Id)).Should().BeTrue();

        // One invite maps to one grant, so a replay of the consumed token must be unambiguously dead
        // rather than resolving to a revoked grant that reads as "you are in".
        var row = await ctx.CircleInvites.SingleAsync();
        row.Status.Should().Be(ShareGrantStatuses.Revoked);

        var replay = await Service(ctx, member).AcceptInviteAsync(invite.Token);
        replay.Status.Should().Be(ShareInviteAcceptStatus.Invalid);
    }

    [Fact]
    public async Task Revoking_AGrantThatIsNotYours_Should_ReportNotFound_WithoutSayingWhy()
    {
        await using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var grant = await Service(ctx, owner).CreateGrantAsync(
            new CreateShareGrantCommand("entities", ShareResourceKinds.CareEntity, [entityId]));

        // False, not an exception carrying detail: the caller's 404 must not distinguish "no such
        // grant" from "not yours", or revocation becomes a way to probe which ids exist.
        (await Service(ctx, Guid.NewGuid()).RevokeAsync(grant.Id)).Should().BeFalse();
    }
}
