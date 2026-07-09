using Aonik.PersonalFinance.Contracts.Models;
using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Services;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Platform;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 048 acceptance: entity-scoped sharing, the fail-closed visibility filter,
/// the docs-only amount-free view (the security property), revoke-immediacy,
/// invite accept, and the Support Statement projection.
/// </summary>
public class CircleServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
        public Guid GetCurrentTenantId() => _tenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId) { tenantId = _tenantId; return true; }
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;
        public TestCurrentUserProvider(Guid userId) => _userId = userId;
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId) { userId = _userId; return true; }
    }

    private sealed class FakeDocumentLinkReader : IDocumentLinkReader
    {
        public List<DocumentRef> OwnerDocs { get; } = [];

        public Task<IReadOnlyList<DocumentRef>> GetForTargetAsync(string targetType, Guid targetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentRef>>([]);
        public Task<IReadOnlyList<DocumentRef>> GetForOwnerTargetAsync(Guid ownerUserId, string targetType, Guid targetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentRef>>(OwnerDocs);
        public Task<IReadOnlyDictionary<Guid, int>> CountForEntitiesAsync(IReadOnlyList<Guid> careEntityIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class FakePartyReader : IPartyReader
    {
        public Dictionary<Guid, string> Names { get; } = new();

        public Task<IReadOnlyList<PartyHistoryItem>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> partyIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartyHistoryItem>>(
                partyIds.Where(Names.ContainsKey).Select(id => new PartyHistoryItem(id, Names[id], "Active", null)).ToList());

        public Task<IReadOnlyList<PartyRelationshipHistoryItem>> GetRelationshipsForPartyAsync(Guid tenantId, Guid partyId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PartyRelationshipHistoryItem>>([]);
        public Task<bool> ExistsAsync(Guid tenantId, Guid partyId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<bool> HasActiveRelationshipBetweenAsync(Guid tenantId, Guid a, Guid b, CancellationToken ct = default) => Task.FromResult(false);
        public Task<Guid?> GetTenantPartyIdAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<Guid?>(null);
    }

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly FakePartyReader _partyReader = new();

    private PersonalFinanceDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"Circle_{Guid.NewGuid()}").Options, new TestTenantProvider(_tenantId));

    private CircleService Circle(
        PersonalFinanceDbContext ctx,
        Guid userId,
        IDocumentLinkReader? documentLinkReader = null,
        InvitePreviewDisclosure disclosure = InvitePreviewDisclosure.Names)
        => new(
            ctx,
            new TestTenantProvider(_tenantId),
            new TestTenantContext(),
            new TestCurrentUserProvider(userId),
            documentLinkReader ?? new FakeDocumentLinkReader(),
            _partyReader,
            Microsoft.Extensions.Options.Options.Create(new CircleInviteOptions { PreviewDisclosure = disclosure }));

    /// <summary>Seeds the owner's PersonalProfile (UserId → PartyId) and registers the party display name for preview.</summary>
    private async Task SeedOwnerProfileAsync(PersonalFinanceDbContext ctx, Guid ownerUserId, string displayName)
    {
        var partyId = Guid.NewGuid();
        ctx.PersonalProfiles.Add(new PersonalProfile { Id = Guid.NewGuid(), TenantId = _tenantId, UserId = ownerUserId, PartyId = partyId });
        await ctx.SaveChangesAsync();
        _partyReader.Names[partyId] = displayName;
    }

    private SupportStatementService Statement(PersonalFinanceDbContext ctx, Guid userId)
        => new(ctx, new TestTenantProvider(_tenantId), new TestCurrentUserProvider(userId), new FakeDocumentLinkReader());

    private async Task<Guid> SeedEntityAsync(PersonalFinanceDbContext ctx, Guid ownerUserId, string name = "Surulere flat")
    {
        var id = Guid.NewGuid();
        ctx.CareEntities.Add(new CareEntity
        {
            Id = id,
            TenantId = _tenantId,
            UserId = ownerUserId,
            Kind = "asset",
            AssetType = "property",
            Name = name,
            CountryCode = "NG",
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    private async Task SeedLogAsync(PersonalFinanceDbContext ctx, Guid ownerUserId, Guid entityId, decimal amount, string currency, DateTime date, string corroborationStatus = "none")
    {
        ctx.PaymentLogs.Add(new PaymentLog
        {
            Id = Guid.NewGuid(),
            TenantId = _tenantId,
            UserId = ownerUserId,
            CareEntityId = entityId,
            Amount = amount,
            Currency = currency,
            Date = date,
            Channel = "bank",
            Origin = "manual",
            CorroborationStatus = corroborationStatus,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task EntityScopeGrant_LetsMemberSeeFullEntity_WithAmounts()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28));

        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { entityId }, false));

        var result = await Circle(ctx, member).GetSharedEntityAsync(owner, entityId);

        result.Should().NotBeNull();
        result!.Scope.Should().Be("entities");
        result.Full.Should().NotBeNull();
        result.DocsOnly.Should().BeNull();
        result.Full!.YearTotals.Should().Contain(t => t.Currency == "GBP" && t.Total == 200m);
    }

    [Fact]
    public async Task DocsOnlyGrant_ReturnsOwnerLinkedDocs_AndNoAmounts()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        // The owner has amounts on this entity — they must NOT leak through a docsOnly share.
        await SeedLogAsync(ctx, owner, entityId, 500m, "GBP", new DateTime(2026, 5, 1));

        await Circle(ctx, owner).CreateGrantAsync(
            new CreateCircleGrantRequest(member, "docsOnly", new[] { entityId }, true));

        // The owner's linked docs surface through the owner-scoped reader (Spec 046 cross-module read).
        var reader = new FakeDocumentLinkReader();
        reader.OwnerDocs.Add(new DocumentRef(Guid.NewGuid(), "Tenancy agreement", "tenancy", "lease.pdf", null));

        var result = await Circle(ctx, member, reader).GetSharedEntityAsync(owner, entityId);

        result.Should().NotBeNull();
        result!.Scope.Should().Be("docsOnly");
        result.Full.Should().BeNull();
        result.DocsOnly.Should().NotBeNull();
        result.DocsOnly!.Documents.Should().ContainSingle(d => d.Title == "Tenancy agreement");
    }

    [Fact]
    public async Task EntitiesGrant_WithNoAmounts_ReturnsEntity_ButSuppressesTotalsAndLogs()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28));

        // scope=entities but NoAmounts=true → the member may see the entity, never the money.
        await Circle(ctx, owner).CreateGrantAsync(
            new CreateCircleGrantRequest(member, "entities", new[] { entityId }, true));

        var result = await Circle(ctx, member).GetSharedEntityAsync(owner, entityId);

        result.Should().NotBeNull();
        result!.Full.Should().NotBeNull();              // entity detail preserved
        result.Full!.Entity.Id.Should().Be(entityId);
        result.Full.YearTotals.Should().BeEmpty();      // amounts suppressed
        result.Full.RecentLogs.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleGrants_AreMerged_SoEverySharedEntityIsVisible()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var flat = await SeedEntityAsync(ctx, owner, "Flat");
        var mum = await SeedEntityAsync(ctx, owner, "Mum");

        // Two separate active grants to the same member (e.g. shared one entity, later another).
        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { flat }, false));
        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { mum }, false));

        var shared = await Circle(ctx, member).ListSharedEntitiesAsync(owner);
        shared.Should().NotBeNull();
        shared!.Select(e => e.Id).Should().Contain(new[] { flat, mum });

        // Both resolve individually — the later grant is not ignored.
        (await Circle(ctx, member).GetSharedEntityAsync(owner, flat)).Should().NotBeNull();
        (await Circle(ctx, member).GetSharedEntityAsync(owner, mum)).Should().NotBeNull();
    }

    [Fact]
    public async Task DocsOnlyEntity_DoesNotInheritAmounts_FromAnotherEntitiesGrant()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var flat = await SeedEntityAsync(ctx, owner, "Flat");
        var mum = await SeedEntityAsync(ctx, owner, "Mum");
        await SeedLogAsync(ctx, owner, mum, 200m, "GBP", new DateTime(2026, 5, 1));

        // Flat shared with amounts; Mum shared docsOnly. The flat grant must not leak Mum's amounts.
        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { flat }, false));
        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "docsOnly", new[] { mum }, true));

        var mumView = await Circle(ctx, member).GetSharedEntityAsync(owner, mum);

        mumView.Should().NotBeNull();
        mumView!.DocsOnly.Should().NotBeNull(); // amount-free projection
        mumView.Full.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvite_IsIdempotentForSameUser_ButSingleUseAcrossUsers()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var other = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        var first = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        first.Status.Should().Be(AcceptInviteStatus.Accepted);

        // Idempotent (Spec 061 §7): the SAME user replaying the parked token gets the SAME grant back
        // (200), never a 404, and no second grant is minted — the resume can fire more than once.
        var replay = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        replay.Status.Should().Be(AcceptInviteStatus.Accepted);
        replay.Grant!.Id.Should().Be(first.Grant!.Id);
        (await ctx.CircleGrants.CountAsync(g => g.MemberUserId == member)).Should().Be(1);

        // Single-use across users: a DIFFERENT user reaching the consumed token is fail-closed (404).
        var stranger = await Circle(ctx, other).AcceptInviteAsync(invite.Token);
        stranger.Status.Should().Be(AcceptInviteStatus.Invalid);
        stranger.Grant.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvite_ReplayAfterRevoke_IsFailClosed_NotAStaleGrant()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        var accepted = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        accepted.Status.Should().Be(AcceptInviteStatus.Accepted);

        // The owner revokes the member's grant.
        await Circle(ctx, owner).RevokeGrantAsync(accepted.Grant!.Id);

        // Replaying the (still-consumed) token must NOT resurface the now-revoked grant as a 200 "you're in".
        // A revoked grant confers nothing, so the honest, fail-closed answer is Invalid (404).
        var replay = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        replay.Status.Should().Be(AcceptInviteStatus.Invalid);
        replay.Grant.Should().BeNull();
    }

    [Fact]
    public async Task AcceptInvite_ByOwner_IsSelfAcceptConflict()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        // The owner tapping their own link: a state conflict (409), not a bad token (404).
        var result = await Circle(ctx, owner).AcceptInviteAsync(invite.Token);

        result.Status.Should().Be(AcceptInviteStatus.SelfAccept);
        result.Grant.Should().BeNull();
        // The invite is untouched — no grant minted, still pending for a real member.
        (await ctx.CircleGrants.CountAsync()).Should().Be(0);
        (await ctx.CircleInvites.FirstAsync()).Status.Should().Be("pending");
    }

    [Fact]
    public async Task RevokeInvite_ByOwner_MarksTokenDead_PreviewAndAcceptFailClosed()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        var revoked = await Circle(ctx, owner).RevokeInviteAsync(invite.Id);
        revoked.Should().BeTrue();
        (await ctx.CircleInvites.FirstAsync()).Status.Should().Be("revoked");

        // The rescinded token is now dead for BOTH the anonymous preview and an authenticated accept.
        (await Circle(ctx, owner).PreviewInviteAsync(invite.Token)).Should().BeNull();
        var accept = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        accept.Status.Should().Be(AcceptInviteStatus.Invalid);
        (await ctx.CircleGrants.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RevokeInvite_ByNonOwner_IsNotFound()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        // A different user cannot rescind someone else's invite — not-found (existence not revealed).
        (await Circle(ctx, stranger).RevokeInviteAsync(invite.Id)).Should().BeFalse();
        (await ctx.CircleInvites.FirstAsync()).Status.Should().Be("pending");
    }

    [Fact]
    public async Task RevokeInvite_IsIdempotent()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        (await Circle(ctx, owner).RevokeInviteAsync(invite.Id)).Should().BeTrue();
        // A retried DELETE on the already-revoked invite is a no-op success, not an error.
        (await Circle(ctx, owner).RevokeInviteAsync(invite.Id)).Should().BeTrue();
        (await ctx.CircleInvites.FirstAsync()).Status.Should().Be("revoked");
    }

    [Fact]
    public async Task RevokeInvite_AlreadyAccepted_ThrowsInvalidState()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));
        await Circle(ctx, member).AcceptInviteAsync(invite.Token);

        // An accepted invite is a spent token; rescinding it as "pending" is a state conflict (422).
        // The owner cuts the live share by revoking the grant instead.
        var act = () => Circle(ctx, owner).RevokeInviteAsync(invite.Id);
        await act.Should().ThrowAsync<InvalidStateException>();
    }

    [Fact]
    public async Task RevokeGrant_CascadesToOriginatingInvite()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));
        var accepted = await Circle(ctx, member).AcceptInviteAsync(invite.Token);

        // Revoking the grant flips the originating invite to "revoked" too — one invite, one grant,
        // one coherent audit trail; the consumed token is left unambiguously dead.
        (await Circle(ctx, owner).RevokeGrantAsync(accepted.Grant!.Id)).Should().BeTrue();
        (await ctx.CircleInvites.FirstAsync(i => i.Id == invite.Id)).Status.Should().Be("revoked");

        var replay = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        replay.Status.Should().Be(AcceptInviteStatus.Invalid);
    }

    [Fact]
    public async Task DocsOnlyGrant_ReturnsAmountFreeView_TheSecurityProperty()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28));

        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "docsOnly", new[] { entityId }, false));

        var result = await Circle(ctx, member).GetSharedEntityAsync(owner, entityId);

        result.Should().NotBeNull();
        result!.Scope.Should().Be("docsOnly");
        result.Full.Should().BeNull();          // no full view → no amounts in the object graph
        result.DocsOnly.Should().NotBeNull();
        result.DocsOnly!.CareEntityId.Should().Be(entityId);
    }

    [Fact]
    public async Task GetSharedEntity_OutOfScope_ReturnsNull()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var sharedEntity = await SeedEntityAsync(ctx, owner, "Shared flat");
        var privateEntity = await SeedEntityAsync(ctx, owner, "Private car");
        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { sharedEntity }, false));

        var result = await Circle(ctx, member).GetSharedEntityAsync(owner, privateEntity);

        result.Should().BeNull(); // out of scope → not found
    }

    [Fact]
    public async Task NoGrant_ResolvesNull_AndSharedReadIsNull()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        (await Circle(ctx, stranger).ResolveAsync(owner)).Should().BeNull();
        (await Circle(ctx, stranger).GetSharedEntityAsync(owner, entityId)).Should().BeNull();
    }

    [Fact]
    public async Task Revoke_DeniesMemberImmediately()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var grant = await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { entityId }, false));

        (await Circle(ctx, member).ResolveAsync(owner)).Should().NotBeNull();
        await Circle(ctx, owner).RevokeGrantAsync(grant.Id);

        (await Circle(ctx, member).ResolveAsync(owner)).Should().BeNull();
        (await Circle(ctx, member).GetSharedEntityAsync(owner, entityId)).Should().BeNull();
    }

    [Fact]
    public async Task Invite_Accept_BindsActiveGrant()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        var result = await Circle(ctx, member).AcceptInviteAsync(invite.Token);

        result.Status.Should().Be(AcceptInviteStatus.Accepted);
        result.Grant!.MemberUserId.Should().Be(member);
        result.Grant.Status.Should().Be("active");
        (await Circle(ctx, member).ResolveAsync(owner)).Should().NotBeNull();
    }

    [Fact]
    public async Task SupportStatement_Composes_PerCurrencyTotals_NeverConverted()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner, "Mum");
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28));
        await SeedLogAsync(ctx, owner, entityId, 85000m, "NGN", new DateTime(2026, 5, 30));

        var statement = await Statement(ctx, owner).ComposeAsync(entityId, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), "HMRC");

        statement.Should().NotBeNull();
        statement!.Rows.Should().HaveCount(2);
        statement.Totals.Should().Contain(t => t.Currency == "GBP" && t.Total == 200m);
        statement.Totals.Should().Contain(t => t.Currency == "NGN" && t.Total == 85000m);
        statement.PreparedFor.Should().Be("HMRC");
        statement.VerificationCode.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task SupportStatement_ReturnsNull_For_OtherUsersEntity()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var statement = await Statement(ctx, stranger).ComposeAsync(entityId, new DateTime(2026, 1, 1), new DateTime(2026, 12, 31), null);

        statement.Should().BeNull();
    }

    // ── Shared expenses: the full paged list + per-expense status ───────

    [Fact]
    public async Task GetSharedEntity_RecentLogs_CarryCorroborationStatus()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28), "confirmed");

        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { entityId }, false));

        var result = await Circle(ctx, member).GetSharedEntityAsync(owner, entityId);

        result!.Full!.RecentLogs.Should().ContainSingle()
            .Which.CorroborationStatus.Should().Be("confirmed");
    }

    [Fact]
    public async Task GetSharedPaymentLogs_EntityScope_PagesAllExpenses_NewestFirst_WithStatus()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 10m, "GBP", new DateTime(2026, 1, 10));
        await SeedLogAsync(ctx, owner, entityId, 20m, "GBP", new DateTime(2026, 2, 10));
        await SeedLogAsync(ctx, owner, entityId, 30m, "GBP", new DateTime(2026, 3, 10), "confirmed");

        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { entityId }, false));

        // Page 1 of 2 — newest first, more to come.
        var page1 = await Circle(ctx, member).GetSharedPaymentLogsAsync(owner, entityId, page: 1, pageSize: 2);
        page1.Should().NotBeNull();
        page1!.Items.Should().HaveCount(2);
        page1.HasMore.Should().BeTrue();
        page1.Items[0].Date.Should().Be(new DateTime(2026, 3, 10));
        page1.Items[0].CorroborationStatus.Should().Be("confirmed"); // status surfaced, not just the amount

        // Page 2 — the tail, no more.
        var page2 = await Circle(ctx, member).GetSharedPaymentLogsAsync(owner, entityId, page: 2, pageSize: 2);
        page2!.Items.Should().HaveCount(1);
        page2.HasMore.Should().BeFalse();
        page2.Items[0].Amount.Should().Be(10m);
    }

    [Fact]
    public async Task GetSharedPaymentLogs_DocsOnly_ReturnsNull_NoAmountLeak()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28));

        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "docsOnly", new[] { entityId }, true));

        (await Circle(ctx, member).GetSharedPaymentLogsAsync(owner, entityId, 1, 20)).Should().BeNull();
    }

    [Fact]
    public async Task GetSharedPaymentLogs_EntitiesGrant_WithNoAmounts_ReturnsNull()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);
        await SeedLogAsync(ctx, owner, entityId, 200m, "GBP", new DateTime(2026, 5, 28));

        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { entityId }, true));

        (await Circle(ctx, member).GetSharedPaymentLogsAsync(owner, entityId, 1, 20)).Should().BeNull();
    }

    [Fact]
    public async Task GetSharedPaymentLogs_OutOfScope_OrNoGrant_ReturnsNull()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        var shared = await SeedEntityAsync(ctx, owner, "Shared flat");
        var privateEntity = await SeedEntityAsync(ctx, owner, "Private car");
        await Circle(ctx, owner).CreateGrantAsync(new CreateCircleGrantRequest(member, "entities", new[] { shared }, false));

        (await Circle(ctx, member).GetSharedPaymentLogsAsync(owner, privateEntity, 1, 20)).Should().BeNull(); // out of scope
        (await Circle(ctx, stranger).GetSharedPaymentLogsAsync(owner, shared, 1, 20)).Should().BeNull();       // no grant
    }

    // ── Spec 061: anonymous invite preview ──────────────────────────────

    [Fact]
    public async Task Preview_ValidPendingInvite_ReturnsOwnerNameScopeAndEntityNames_WithNoAmounts()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        await SeedOwnerProfileAsync(ctx, owner, "Ama O.");
        var mum = await SeedEntityAsync(ctx, owner, "Mum");
        var flat = await SeedEntityAsync(ctx, owner, "Surulere flat");
        // Amounts on the shared entities — they must never surface in a preview.
        await SeedLogAsync(ctx, owner, mum, 200m, "GBP", new DateTime(2026, 5, 28));

        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { mum, flat }, false, "link"));

        // No current user (anonymous): resolved purely from the token.
        var preview = await Circle(ctx, Guid.NewGuid()).PreviewInviteAsync(invite.Token);

        preview.Should().NotBeNull();
        preview!.OwnerDisplayName.Should().Be("Ama O.");
        preview.Scope.Should().Be("entities");
        preview.ScopeLabel.Should().Be("Selected people & places");
        preview.EntityNames.Should().BeEquivalentTo(new[] { "Mum", "Surulere flat" });
        preview.EntityCount.Should().Be(2);
        preview.ExpiresAt.Should().Be(invite.ExpiresAt);
        // No-amounts property: InvitePreviewResponse structurally has no amount / balance / corroboration /
        // member / document field, so there is nothing to leak — the only disclosure is plain entity names.
        typeof(InvitePreviewResponse).GetProperties().Select(p => p.Name)
            .Should().BeEquivalentTo(new[]
            {
                nameof(InvitePreviewResponse.OwnerDisplayName), nameof(InvitePreviewResponse.Scope),
                nameof(InvitePreviewResponse.ScopeLabel), nameof(InvitePreviewResponse.EntityNames),
                nameof(InvitePreviewResponse.EntityCount), nameof(InvitePreviewResponse.NoAmounts),
                nameof(InvitePreviewResponse.ExpiresAt),
            });
    }

    [Fact]
    public async Task Preview_ScopeAll_OmitsEntityNames_AndLabelsEverything()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        await SeedOwnerProfileAsync(ctx, owner, "Ama O.");
        await SeedEntityAsync(ctx, owner, "Mum");
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("all", null, false, "link"));

        var preview = await Circle(ctx, Guid.NewGuid()).PreviewInviteAsync(invite.Token);

        preview!.Scope.Should().Be("all");
        preview.ScopeLabel.Should().Be("Everything they look after");
        preview.EntityNames.Should().BeEmpty(); // share-all has no specific list to name
        preview.EntityCount.Should().Be(0);
    }

    [Fact]
    public async Task Preview_CountsDisclosure_HidesNames_KeepsCount()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        await SeedOwnerProfileAsync(ctx, owner, "Ama O.");
        var mum = await SeedEntityAsync(ctx, owner, "Mum");
        var flat = await SeedEntityAsync(ctx, owner, "Surulere flat");
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { mum, flat }, false, "link"));

        var preview = await Circle(ctx, Guid.NewGuid(), disclosure: InvitePreviewDisclosure.Counts)
            .PreviewInviteAsync(invite.Token);

        preview!.EntityNames.Should().BeEmpty();  // dialled back to counts
        preview.EntityCount.Should().Be(2);        // the count still renders "2 people & places"
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("consumed")]
    [InlineData("revoked")]
    [InlineData("unknown")]
    public async Task Preview_FailClosed_IsIndistinguishableNull_ForEveryNonPendingToken(string kind)
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        await SeedOwnerProfileAsync(ctx, owner, "Ama O.");
        var entityId = await SeedEntityAsync(ctx, owner);
        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        var token = invite.Token;
        if (kind == "unknown")
        {
            token = "this-token-does-not-exist";
        }
        else
        {
            var row = await ctx.CircleInvites.FirstAsync(i => i.Token == token);
            switch (kind)
            {
                case "expired": row.ExpiresAt = DateTime.UtcNow.AddDays(-1); break;
                case "consumed": row.Status = "accepted"; break;
                case "revoked": row.Status = "revoked"; break;
            }
            await ctx.SaveChangesAsync();
        }

        var preview = await Circle(ctx, Guid.NewGuid()).PreviewInviteAsync(token);

        preview.Should().BeNull(); // one indistinguishable null → one 404 for every case (no oracle)
    }
}
