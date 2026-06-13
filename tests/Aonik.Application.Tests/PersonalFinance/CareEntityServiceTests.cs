using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 043 acceptance: customer CRUD, asset generality + kind/assetType
/// invariant, per-user isolation (not-found, never revealed), soft archive,
/// attribute round-trip, and the one-call profile that degrades to empty
/// dependent arrays before Specs 044–046 land.
/// </summary>
public class CareEntityServiceTests
{
    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;
        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;
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
        public TestCurrentUserProvider(Guid userId) => _userId = userId;
        public Guid? GetCurrentUserId() => _userId;
        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static CareEntityService CreateService(PersonalFinanceDbContext context, Guid tenantId, Guid userId)
        => new(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId));

    private static PaymentLogService CreatePaymentLogService(PersonalFinanceDbContext context, Guid tenantId, Guid userId)
        => new(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId));

    private static CommitmentService CreateCommitmentService(PersonalFinanceDbContext context, Guid tenantId, Guid userId)
        => new(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId),
            CreatePaymentLogService(context, tenantId, userId), new FakeTaskService(), NullLogger<CommitmentService>.Instance);

    private sealed class FakeTaskService : ITaskService
    {
        public Task<TaskResponse> ScheduleAsync(ScheduleTaskRequest request, CancellationToken ct = default) => Task.FromResult<TaskResponse>(null!);
        public Task<TaskResponse?> GetAsync(Guid taskId, CancellationToken ct = default) => Task.FromResult<TaskResponse?>(null);
        public Task<IReadOnlyList<TaskResponse>> ListForSubjectAsync(string subjectType, Guid subjectId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskResponse>>([]);
        public Task<IReadOnlyList<TaskResponse>> ListForAssigneeAsync(string assigneeType, Guid? assigneeId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskResponse>>([]);
        public Task PauseAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task ResumeAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
        public Task CancelAsync(Guid taskId, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeDocumentLinkReader : IDocumentLinkReader
    {
        public Task<IReadOnlyList<DocumentRef>> GetForTargetAsync(string targetType, Guid targetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentRef>>([]);

        public Task<IReadOnlyList<DocumentRef>> GetForOwnerTargetAsync(Guid ownerUserId, string targetType, Guid targetId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DocumentRef>>([]);

        public Task<IReadOnlyDictionary<Guid, int>> CountForEntitiesAsync(IReadOnlyList<Guid> careEntityIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());
    }

    private static CreateCareEntityRequest PersonRequest(string name = "Mum", string country = "NG")
        => new("person", null, name, country, "mother", "👩🏾", null, null);

    private static CreateCareEntityRequest AssetRequest(
        string assetType = "property",
        string name = "Surulere flat",
        string country = "NG",
        IReadOnlyDictionary<string, string>? attributes = null)
        => new("asset", assetType, name, country, null, "🏠", null, attributes);

    // ── Create ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_Should_PersistPerson_When_KindIsPerson()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var result = await service.CreateAsync(PersonRequest());

        result.Id.Should().NotBeEmpty();
        result.Kind.Should().Be("person");
        result.AssetType.Should().BeNull();
        result.Name.Should().Be("Mum");
        result.CountryCode.Should().Be("NG");
        result.Relationship.Should().Be("mother");
        result.Archived.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_Should_PersistAsset_AndRoundTripAttributes_When_KindIsAsset()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var attributes = new Dictionary<string, string>
        {
            ["registration"] = "LAG-123-AB",
            ["colour"] = "silver",
        };

        var result = await service.CreateAsync(AssetRequest("vehicle", "The Hilux", "NG", attributes));

        result.Kind.Should().Be("asset");
        result.AssetType.Should().Be("vehicle");
        result.Name.Should().Be("The Hilux");
        result.Attributes.Should().HaveCount(2);
        result.Attributes["registration"].Should().Be("LAG-123-AB");
        result.Attributes["colour"].Should().Be("silver");
    }

    [Theory]
    [InlineData("property")]
    [InlineData("land")]
    [InlineData("vehicle")]
    [InlineData("business")]
    [InlineData("account")]
    [InlineData("other")]
    public async Task CreateAsync_Should_AcceptAllCuratedAssetTypes(string assetType)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var result = await service.CreateAsync(AssetRequest(assetType, $"asset-{assetType}"));

        result.AssetType.Should().Be(assetType);
    }

    [Fact]
    public async Task CreateAsync_Should_NormalizeCountryAndAssetTypeCasing()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var result = await service.CreateAsync(
            new CreateCareEntityRequest("asset", "Property", "Flat", "ng", null, null, null, null));

        result.CountryCode.Should().Be("NG");
        result.AssetType.Should().Be("property");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_AssetHasNoAssetType()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var request = new CreateCareEntityRequest("asset", null, "Mystery", "NG", null, null, null, null);

        var act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_PersonHasAssetType()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var request = new CreateCareEntityRequest("person", "property", "Mum", "NG", null, null, null, null);

        var act = async () => await service.CreateAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── List ────────────────────────────────────────────────────────

    [Fact]
    public async Task ListAsync_Should_ReturnOnlyOwnerEntities()
    {
        var tenantId = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var serviceA = CreateService(context, tenantId, userA);
        var serviceB = CreateService(context, tenantId, userB);

        await serviceA.CreateAsync(PersonRequest("Mum"));
        await serviceA.CreateAsync(AssetRequest("property", "Flat"));
        await serviceB.CreateAsync(PersonRequest("Dad"));

        var aList = await serviceA.ListAsync();
        var bList = await serviceB.ListAsync();

        aList.Should().HaveCount(2);
        bList.Should().ContainSingle().Which.Name.Should().Be("Dad");
    }

    [Fact]
    public async Task ListAsync_Should_ExcludeArchivedByDefault_ButIncludeWhenRequested()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var keep = await service.CreateAsync(PersonRequest("Mum"));
        var gone = await service.CreateAsync(PersonRequest("Aunty"));
        await service.ArchiveAsync(gone.Id);

        var defaultList = await service.ListAsync();
        var withArchived = await service.ListAsync(includeArchived: true);

        defaultList.Should().ContainSingle().Which.Id.Should().Be(keep.Id);
        withArchived.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListAsync_Should_FilterByKind()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        await service.CreateAsync(PersonRequest("Mum"));
        await service.CreateAsync(AssetRequest("land", "Family land"));

        var assets = await service.ListAsync(kind: "asset");

        assets.Should().ContainSingle().Which.AssetType.Should().Be("land");
    }

    // ── Isolation (404, not 403 — existence not revealed) ────────────

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_EntityBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var ownerService = CreateService(context, tenantId, owner);
        var strangerService = CreateService(context, tenantId, stranger);

        var created = await ownerService.CreateAsync(PersonRequest("Mum"));

        var seen = await strangerService.GetAsync(created.Id);

        seen.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Should_ReturnNull_When_EntityBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var ownerService = CreateService(context, tenantId, owner);
        var strangerService = CreateService(context, tenantId, stranger);

        var created = await ownerService.CreateAsync(PersonRequest("Mum"));

        var updated = await strangerService.UpdateAsync(
            created.Id,
            new UpdateCareEntityRequest("Hacked", null, "NG", null, null, null, null));

        updated.Should().BeNull();
    }

    [Fact]
    public async Task ArchiveAsync_Should_ReturnFalse_When_EntityBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var ownerService = CreateService(context, tenantId, owner);
        var strangerService = CreateService(context, tenantId, stranger);

        var created = await ownerService.CreateAsync(PersonRequest("Mum"));

        var archived = await strangerService.ArchiveAsync(created.Id);

        archived.Should().BeFalse();
        (await ownerService.GetAsync(created.Id))!.Archived.Should().BeFalse();
    }

    // ── Update ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Should_UpdateMutableFields_When_Owned()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var created = await service.CreateAsync(PersonRequest("Mum", "NG"));

        var updated = await service.UpdateAsync(
            created.Id,
            new UpdateCareEntityRequest("Mama", null, "GB", "mother-in-law", "👵🏾", null, null));

        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Mama");
        updated.CountryCode.Should().Be("GB");
        updated.Relationship.Should().Be("mother-in-law");
        updated.Kind.Should().Be("person"); // kind is immutable
    }

    [Fact]
    public async Task UpdateAsync_Should_Throw_When_PersonGivenAssetType()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var created = await service.CreateAsync(PersonRequest("Mum"));

        var act = async () => await service.UpdateAsync(
            created.Id,
            new UpdateCareEntityRequest("Mum", "property", "NG", null, null, null, null));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Profile (§8) — degrades to empty dependent arrays pre-044/046 ─

    [Fact]
    public async Task GetProfileAsync_Should_ReturnEntityWithEmptyDependentArrays_When_Owned()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);
        var profileService = new CareEntityProfileService(service, CreatePaymentLogService(context, tenantId, userId), CreateCommitmentService(context, tenantId, userId), new FakeDocumentLinkReader());

        var created = await service.CreateAsync(AssetRequest("property", "Surulere flat"));

        var profile = await profileService.GetProfileAsync(created.Id);

        profile.Should().NotBeNull();
        profile!.Entity.Id.Should().Be(created.Id);
        profile.YearTotals.Should().BeEmpty();
        profile.Commitments.Should().BeEmpty();
        profile.RecentLogs.Should().BeEmpty();
        profile.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileAsync_Should_ReturnNull_When_EntityBelongsToAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var ownerService = CreateService(context, tenantId, owner);
        var strangerProfileService = new CareEntityProfileService(
            CreateService(context, tenantId, stranger),
            CreatePaymentLogService(context, tenantId, stranger),
            CreateCommitmentService(context, tenantId, stranger),
            new FakeDocumentLinkReader());

        var created = await ownerService.CreateAsync(PersonRequest("Mum"));

        var profile = await strangerProfileService.GetProfileAsync(created.Id);

        profile.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAsync_Should_IncludeOpenCommitments_ForEntity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);
        var profileService = new CareEntityProfileService(
            service,
            CreatePaymentLogService(context, tenantId, userId),
            CreateCommitmentService(context, tenantId, userId),
            new FakeDocumentLinkReader());

        var entity = await service.CreateAsync(PersonRequest("Mum"));
        await CreateCommitmentService(context, tenantId, userId).CreateSupportAsync(
            new CreateSupportCommitmentRequest(
                entity.Id, "Mum — monthly allowance", 200m, "GBP", "Monthly", 1, 28,
                null, new DateTime(2026, 5, 28), 3, null, null));

        var profile = await profileService.GetProfileAsync(entity.Id);

        profile.Should().NotBeNull();
        profile!.Commitments.Should().ContainSingle(c => c.DisplayName == "Mum — monthly allowance");
    }
}
