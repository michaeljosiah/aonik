using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Documents;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 045 acceptance: attributed acts, original-currency totals (never
/// converted), idempotent offline create, confirm-gated corroboration link +
/// dedup, soft-delete + 30-day restore, and per-user isolation.
/// </summary>
public class PaymentLogServiceTests
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

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static PaymentLogService CreateService(PersonalFinanceDbContext context, Guid tenantId, Guid userId)
        => new(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId));

    private static async Task<Guid> SeedCareEntityAsync(PersonalFinanceDbContext context, Guid tenantId, Guid userId, string name = "Mum")
    {
        var careService = new CareEntityService(context, new TestTenantProvider(tenantId), new TestCurrentUserProvider(userId), new Mock<IDocumentReader>().Object, NullLogger<CareEntityService>.Instance);
        var created = await careService.CreateAsync(
            new CreateCareEntityRequest("person", null, name, "NG", null, null, null, null));
        return created.Id;
    }

    private static CreatePaymentLogRequest LogRequest(
        Guid careEntityId,
        decimal amount = 200m,
        string currency = "GBP",
        Guid? idempotencyKey = null,
        DateTime? date = null)
        => new(careEntityId, null, null, amount, currency, null,
            date ?? new DateTime(2026, 5, 28), "bank", "manual", null, idempotencyKey);

    private static CreatePaymentLogRequest LogRequestFor(Guid careEntityId, Guid? commitmentId, Guid? cycleId)
        => new(careEntityId, commitmentId, cycleId, 200m, "GBP", null,
            new DateTime(2026, 6, 28), "bank", "markDone", null, null);

    private static async Task<(Guid CommitmentId, Guid CycleId)> SeedCommitmentWithCycleAsync(
        PersonalFinanceDbContext context, Guid tenantId, Guid userId, Guid careEntityId)
    {
        var commitmentId = Guid.NewGuid();
        context.Set<PersonalRecurringBill>().Add(new PersonalRecurringBill
        {
            Id = commitmentId,
            TenantId = tenantId,
            UserId = userId,
            CareEntityId = careEntityId,
            CommitmentKind = "Support",
            Payee = "Mum",
            Currency = "GBP",
        });

        var cycleId = Guid.NewGuid();
        context.Set<CommitmentCycle>().Add(new CommitmentCycle
        {
            Id = cycleId,
            TenantId = tenantId,
            UserId = userId,
            CommitmentId = commitmentId,
            DueDate = new DateTime(2026, 6, 28),
            Status = "Open",
        });

        await context.SaveChangesAsync();
        return (commitmentId, cycleId);
    }

    [Fact]
    public async Task CreateAsync_Should_PersistAndAttributeToEntity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var result = await service.CreateAsync(LogRequest(entityId, 85000m, "ngn"));

        result.Id.Should().NotBeEmpty();
        result.CareEntityId.Should().Be(entityId);
        result.Amount.Should().Be(85000m);
        result.Currency.Should().Be("NGN"); // normalised
        result.CorroborationStatus.Should().Be("none");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_CareEntityNotOwned()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, tenantId, userId);

        var act = async () => await service.CreateAsync(LogRequest(Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_CommitmentBelongsToDifferentCareEntity()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var mum = await SeedCareEntityAsync(context, tenantId, userId, "Mum");
        var dad = await SeedCareEntityAsync(context, tenantId, userId, "Dad");
        var (dadsCommitment, _) = await SeedCommitmentWithCycleAsync(context, tenantId, userId, dad);
        var service = CreateService(context, tenantId, userId);

        // A log against Mum that points at Dad's commitment must be rejected.
        var act = async () => await service.CreateAsync(LogRequestFor(mum, dadsCommitment, null));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*does not belong to the specified care entity*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_CycleDoesNotBelongToCommitment()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entity = await SeedCareEntityAsync(context, tenantId, userId);
        var (commitmentId, _) = await SeedCommitmentWithCycleAsync(context, tenantId, userId, entity);
        var service = CreateService(context, tenantId, userId);

        var act = async () => await service.CreateAsync(LogRequestFor(entity, commitmentId, Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*cycle does not belong to the specified commitment*");
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_CycleSuppliedWithoutCommitment()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entity = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        var act = async () => await service.CreateAsync(LogRequestFor(entity, null, Guid.NewGuid()));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*CommitmentCycleId requires CommitmentId*");
    }

    [Fact]
    public async Task CreateAsync_Should_Succeed_When_Commitment_Entity_AndCycle_Agree()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entity = await SeedCareEntityAsync(context, tenantId, userId);
        var (commitmentId, cycleId) = await SeedCommitmentWithCycleAsync(context, tenantId, userId, entity);
        var service = CreateService(context, tenantId, userId);

        var result = await service.CreateAsync(LogRequestFor(entity, commitmentId, cycleId));

        result.CareEntityId.Should().Be(entity);
    }

    [Fact]
    public async Task CreateAsync_Should_BeIdempotent_AfterSoftDelete_ReturningPriorLog()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);
        var key = Guid.NewGuid();

        var created = await service.CreateAsync(LogRequest(entityId, idempotencyKey: key));
        (await service.SoftDeleteAsync(created.Id)).Should().BeTrue();

        // Replaying the same offline create after delete must return the prior log (the unique
        // key still holds), not silently create a duplicate (or, on SQL Server, crash).
        var replay = await service.CreateAsync(LogRequest(entityId, idempotencyKey: key));

        replay.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task CreateAsync_Should_BeIdempotent_OnIdempotencyKey()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);
        var key = Guid.NewGuid();

        var first = await service.CreateAsync(LogRequest(entityId, idempotencyKey: key));
        var second = await service.CreateAsync(LogRequest(entityId, idempotencyKey: key));

        second.Id.Should().Be(first.Id);
        var all = await service.ListAsync(entityId, null, null, 1, 100);
        all.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task GetEntityYearTotals_Should_GroupByCurrency_NeverConverted()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);

        await service.CreateAsync(LogRequest(entityId, 200m, "GBP"));
        await service.CreateAsync(LogRequest(entityId, 85000m, "NGN"));

        var totals = await service.GetEntityYearTotalsAsync(entityId, year: null);

        totals.Should().HaveCount(2);
        totals.Should().Contain(t => t.Currency == "GBP" && t.Total == 200m && t.Count == 1);
        totals.Should().Contain(t => t.Currency == "NGN" && t.Total == 85000m && t.Count == 1);
    }

    [Fact]
    public async Task SoftDelete_Should_HideFromList_AndRestore_Should_BringItBack()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);
        var log = await service.CreateAsync(LogRequest(entityId));

        var deleted = await service.SoftDeleteAsync(log.Id);
        var afterDelete = await service.ListAsync(entityId, null, null, 1, 100);

        var restored = await service.RestoreAsync(log.Id);
        var afterRestore = await service.ListAsync(entityId, null, null, 1, 100);

        deleted.Should().BeTrue();
        afterDelete.Items.Should().BeEmpty();
        restored.Should().NotBeNull();
        afterRestore.Items.Should().ContainSingle().Which.Id.Should().Be(log.Id);
    }

    [Fact]
    public async Task Restore_Should_ReturnNull_When_OutsideWindow()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);
        var log = await service.CreateAsync(LogRequest(entityId));
        await service.SoftDeleteAsync(log.Id);

        // Age the soft-delete past the 30-day restore window.
        var deletedRow = await context.PaymentLogs.IgnoreQueryFilters().FirstAsync(p => p.Id == log.Id);
        deletedRow.DeletedAt = DateTime.UtcNow.AddDays(-40);
        await context.SaveChangesAsync();

        var restored = await service.RestoreAsync(log.Id);

        restored.Should().BeNull();
    }

    [Fact]
    public async Task LinkTransaction_Should_SetConfirmed_AndUnlink_Should_SetNone()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, userId);
        var service = CreateService(context, tenantId, userId);
        var log = await service.CreateAsync(LogRequest(entityId));

        var txId = Guid.NewGuid();
        context.Set<PersonalTransaction>().Add(new PersonalTransaction
        {
            Id = txId,
            TenantId = tenantId,
            UserId = userId,
            Amount = 200m,
            Currency = "GBP",
        });
        await context.SaveChangesAsync();

        var linked = await service.LinkTransactionAsync(log.Id, txId);
        linked!.CorroborationStatus.Should().Be("confirmed");
        linked.SourceTransactionId.Should().Be(txId);

        var unlinked = await service.UnlinkTransactionAsync(log.Id);
        unlinked!.CorroborationStatus.Should().Be("none");
        unlinked.SourceTransactionId.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_OwnedByAnotherUser()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var entityId = await SeedCareEntityAsync(context, tenantId, owner);
        var ownerService = CreateService(context, tenantId, owner);
        var strangerService = CreateService(context, tenantId, stranger);

        var log = await ownerService.CreateAsync(LogRequest(entityId));

        var seen = await strangerService.GetAsync(log.Id);

        seen.Should().BeNull();
    }
}
