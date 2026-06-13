using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

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

    private readonly Guid _tenantId = Guid.NewGuid();

    private PersonalFinanceDbContext CreateContext()
        => new(new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"Circle_{Guid.NewGuid()}").Options, new TestTenantProvider(_tenantId));

    private CircleService Circle(PersonalFinanceDbContext ctx, Guid userId, IDocumentLinkReader? documentLinkReader = null)
        => new(ctx, new TestTenantProvider(_tenantId), new TestCurrentUserProvider(userId), documentLinkReader ?? new FakeDocumentLinkReader());

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

    private async Task SeedLogAsync(PersonalFinanceDbContext ctx, Guid ownerUserId, Guid entityId, decimal amount, string currency, DateTime date)
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
            CorroborationStatus = "none",
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
    public async Task AcceptInvite_ConsumesToken_AndIsNotReusable()
    {
        using var ctx = CreateContext();
        var owner = Guid.NewGuid();
        var member = Guid.NewGuid();
        var entityId = await SeedEntityAsync(ctx, owner);

        var invite = await Circle(ctx, owner).CreateInviteAsync(
            new CreateCircleInviteRequest("entities", new[] { entityId }, false, "link"));

        var grant = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        grant.Should().NotBeNull();

        // The token is consumed atomically with grant creation, so a replay is rejected.
        var second = await Circle(ctx, member).AcceptInviteAsync(invite.Token);
        second.Should().BeNull();
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

        var grant = await Circle(ctx, member).AcceptInviteAsync(invite.Token);

        grant.Should().NotBeNull();
        grant!.MemberUserId.Should().Be(member);
        grant.Status.Should().Be("active");
        (await Circle(ctx, member).ResolveAsync(owner)).Should().NotBeNull();
        // single-use: a second accept fails
        (await Circle(ctx, member).AcceptInviteAsync(invite.Token)).Should().BeNull();
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
}
