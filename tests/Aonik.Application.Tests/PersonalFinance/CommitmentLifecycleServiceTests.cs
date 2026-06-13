using Aonik.Finance.Contracts.Models.PersonalFinance;
using Aonik.Finance.Entities.PersonalFinance;
using Aonik.Finance.Services.PersonalFinance;
using Aonik.PersonalFinance.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.PersonalFinance;

/// <summary>
/// Spec 044 acceptance: author + attach, mark-done rolls forward + writes a
/// PaymentLog, skip is honest, snooze/pause, cycle history, isolation, backfill.
/// </summary>
public class CommitmentLifecycleServiceTests
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

    /// <summary>No-op task service — reminders are best-effort; the lifecycle never depends on them.</summary>
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

    private static PersonalFinanceDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<PersonalFinanceDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new PersonalFinanceDbContext(options, new TestTenantProvider(tenantId));
    }

    private static (CommitmentService Commitments, PaymentLogService Payments, CareEntityService Care) CreateServices(
        PersonalFinanceDbContext context, Guid tenantId, Guid userId)
    {
        var tp = new TestTenantProvider(tenantId);
        var up = new TestCurrentUserProvider(userId);
        var payments = new PaymentLogService(context, tp, up);
        var commitments = new CommitmentService(context, tp, up, payments, new FakeTaskService(), NullLogger<CommitmentService>.Instance);
        var care = new CareEntityService(context, tp, up);
        return (commitments, payments, care);
    }

    private static async Task<Guid> SeedCareEntityAsync(CareEntityService care, string name = "Mum")
    {
        var created = await care.CreateAsync(new CreateCareEntityRequest("person", null, name, "NG", null, null, null, null));
        return created.Id;
    }

    private static CreateSupportCommitmentRequest MonthlyAllowance(Guid entityId, int anchorDay = 28, DateTime? firstDue = null)
        => new(entityId, "Mum — monthly allowance", 200m, "GBP", "Monthly", 1, anchorDay,
            null, firstDue ?? new DateTime(2026, 5, 28), 3, null, null);

    [Fact]
    public async Task CreateSupport_Should_Throw_When_NonExplicitRhythmHasNoFirstDueDate()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);

        // Monthly with a default (DateTime.MinValue) FirstDueDate must be rejected, not opened on 0001-01-01.
        var act = async () => await commitments.CreateSupportAsync(
            new CreateSupportCommitmentRequest(entityId, "Mum", 200m, "GBP", "Monthly", 1, 28, null, default, 3, null, null));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*FirstDueDate is required*");
    }

    [Fact]
    public async Task UpdateSupport_Should_Throw_When_TermlyWithoutTermDates()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));

        // Switching to Termly with no dates would store null and break the next mark-done roll.
        var act = async () => await commitments.UpdateSupportAsync(
            created.CommitmentId,
            new UpdateSupportCommitmentRequest("Mum — allowance", 200m, "GBP", "Termly", 1, null, null, 3, null));

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Termly/OneOff commitments require explicit TermDates*");
    }

    [Fact]
    public async Task CreateSupport_Should_OpenFirstCycle_AndReturnSupportDetail()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);

        var detail = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));

        detail.CommitmentKind.Should().Be("Support");
        detail.CareEntityId.Should().Be(entityId);
        detail.VerificationStatus.Should().Be("Confirmed");
        detail.Origin.Should().Be("Manual");
        detail.RhythmLabel.Should().Be("Monthly · 28th");
        detail.DueDate.Should().Be(new DateTime(2026, 5, 28));

        var cycles = await commitments.GetCyclesAsync(detail.CommitmentId, 1, 20);
        cycles.Should().ContainSingle();
        cycles![0].Status.Should().Be("Open");
        cycles[0].DueDate.Should().Be(new DateTime(2026, 5, 28));
    }

    [Fact]
    public async Task MarkDone_Should_WritePaymentLog_RollForward_AndOpenNextCycle()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, payments, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));

        var done = await commitments.MarkDoneAsync(
            created.CommitmentId,
            new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", "May allowance", null));

        // Rolled forward 28 May → 28 Jun, anchor preserved.
        done!.DueDate.Should().Be(new DateTime(2026, 6, 28));

        var cycles = await commitments.GetCyclesAsync(created.CommitmentId, 1, 20);
        cycles!.Should().HaveCount(2);
        cycles.Should().Contain(c => c.DueDate == new DateTime(2026, 5, 28) && c.Status == "Paid" && c.PaymentLogId != null);
        cycles.Should().Contain(c => c.DueDate == new DateTime(2026, 6, 28) && c.Status == "Open");

        var logs = await payments.ListAsync(entityId, created.CommitmentId, null, 1, 100);
        logs.Items.Should().ContainSingle();
        logs.Items[0].CommitmentCycleId.Should().NotBeNull();
        logs.Items[0].Amount.Should().Be(200m);
    }

    [Fact]
    public async Task MarkDone_Should_BeReplaySafe_OnIdempotencyKey()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, payments, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));
        var key = Guid.NewGuid();

        await commitments.MarkDoneAsync(created.CommitmentId, new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", null, key));
        var replay = await commitments.MarkDoneAsync(created.CommitmentId, new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", null, key));

        // Replay must not advance another cycle or write a second log.
        replay!.DueDate.Should().Be(new DateTime(2026, 6, 28));
        var logs = await payments.ListAsync(entityId, created.CommitmentId, null, 1, 100);
        logs.Items.Should().ContainSingle();
    }

    [Fact]
    public async Task MarkDone_Should_ClampAnchorAcrossShortMonth()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        // 31st-anchored monthly, first due 31 Jan 2026.
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId, anchorDay: 31, firstDue: new DateTime(2026, 1, 31)));

        var done = await commitments.MarkDoneAsync(
            created.CommitmentId, new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", null, null));

        done!.DueDate.Should().Be(new DateTime(2026, 2, 28)); // clamped (2026 not leap)
    }

    [Fact]
    public async Task SkipCycle_Should_RecordSkipped_AndAdvance()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));

        await commitments.SkipCycleAsync(created.CommitmentId, "Tight month");

        var cycles = await commitments.GetCyclesAsync(created.CommitmentId, 1, 20);
        cycles.Should().Contain(c => c.DueDate == new DateTime(2026, 5, 28) && c.Status == "Skipped" && c.SkipReason == "Tight month");
        cycles.Should().Contain(c => c.DueDate == new DateTime(2026, 6, 28) && c.Status == "Open");
    }

    [Fact]
    public async Task Snooze_Should_RecordSnoozedUntil_WithoutResolving()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));
        var until = new DateTime(2026, 6, 1);

        await commitments.SnoozeAsync(created.CommitmentId, until);

        var cycles = await commitments.GetCyclesAsync(created.CommitmentId, 1, 20);
        cycles.Should().ContainSingle(); // not advanced
        cycles![0].Status.Should().Be("Snoozed");
        cycles[0].SnoozedUntil.Should().Be(until);
        cycles[0].ResolvedAt.Should().BeNull();
    }

    [Fact]
    public async Task PauseResume_Should_ToggleStatus()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, care) = CreateServices(context, tenantId, userId);
        var entityId = await SeedCareEntityAsync(care);
        var created = await commitments.CreateSupportAsync(MonthlyAllowance(entityId));

        (await commitments.PauseAsync(created.CommitmentId))!.Status.Should().Be("Paused");
        (await commitments.ResumeAsync(created.CommitmentId))!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task MarkDone_Should_ReturnNull_When_NotOwned()
    {
        var tenantId = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (ownerCommitments, _, ownerCare) = CreateServices(context, tenantId, owner);
        var (strangerCommitments, _, _) = CreateServices(context, tenantId, stranger);
        var entityId = await SeedCareEntityAsync(ownerCare);
        var created = await ownerCommitments.CreateSupportAsync(MonthlyAllowance(entityId));

        var result = await strangerCommitments.MarkDoneAsync(
            created.CommitmentId, new MarkCommitmentDoneRequest(200m, "GBP", null, null, "bank", null, null));

        result.Should().BeNull();
    }

    [Fact]
    public async Task Backfill_Should_OpenOneCyclePerActiveCommitment_Idempotently()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var (commitments, _, _) = CreateServices(context, tenantId, userId);

        // A detected/promoted commitment created directly with no cycle.
        context.Set<PersonalRecurringBill>().Add(new PersonalRecurringBill
        {
            TenantId = tenantId,
            UserId = userId,
            Payee = "Council Tax",
            Status = "Active",
            Currency = "GBP",
            NextDueDate = new DateTime(2026, 6, 1),
            Origin = "Detected",
            VerificationStatus = "Confirmed",
        });
        await context.SaveChangesAsync();

        var firstRun = await commitments.BackfillOpenCyclesAsync();
        var secondRun = await commitments.BackfillOpenCyclesAsync();

        firstRun.Should().Be(1);
        secondRun.Should().Be(0); // idempotent
    }
}
