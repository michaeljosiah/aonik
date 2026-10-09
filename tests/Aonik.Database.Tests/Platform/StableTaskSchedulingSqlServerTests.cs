using Aonik.IntegrationTests.Support;
using Aonik.Platform.Entities.Tasks;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Tasks;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Aonik.Database.Tests.Platform;

public sealed class StableTaskSchedulingSqlServerTests(SqlLocalDbFixture database) : IClassFixture<SqlLocalDbFixture>
{
    [SkippableFact]
    public async Task ConcurrentSameBinding_Should_ConvergeOnOneNativeTask_AndDetachLosingInsert()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var request = Request();
        var barrier = new BeforeInsertBarrier();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        await using var first = Context(tenantId, barrier);
        await using var second = Context(tenantId, barrier);

        // Both absence reads finish before either INSERT; the actual SQL primary key
        // chooses the winner, exercising the service's unique-conflict convergence.
        var results = await Task.WhenAll(
            Service(first, tenantId).ScheduleAsync(request, timeout.Token),
            Service(second, tenantId).ScheduleAsync(request, timeout.Token));

        barrier.Arrivals.Should().Be(2);
        results.Select(x => x.Id).Should().OnlyContain(id => id == request.TaskId);
        results.Select(x => x.TenantId).Should().OnlyContain(id => id == tenantId);
        results.Select(x => x.Status).Should().OnlyContain(status => status == TaskStatuses.Scheduled);
        first.ChangeTracker.Entries<WorkItem>().Should().NotContain(x => x.State == EntityState.Added);
        second.ChangeTracker.Entries<WorkItem>().Should().NotContain(x => x.State == EntityState.Added);
        await first.SaveChangesAsync(timeout.Token);
        await second.SaveChangesAsync(timeout.Token);

        await using var verify = Context(tenantId);
        var rows = await verify.WorkItems.ToListAsync(timeout.Token);
        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(request.TaskId!.Value);
        rows[0].RowVersion.Should().HaveCount(8);
        rows[0].ActionPayloadJson.Should().Be(request.ActionPayloadJson);
        rows[0].NextRunAtUtc.Should().Be(request.RunAtUtc);
        rows[0].StartAtUtc.Should().Be(request.RunAtUtc);
        rows[0].RunCount.Should().Be(0);
    }

    [SkippableTheory]
    [InlineData(TaskStatuses.Completed)]
    [InlineData(TaskStatuses.Cancelled)]
    public async Task ReplayTerminalTask_Should_NotRearmOrChangeNativeVersion(string terminalStatus)
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var request = Request();
        byte[] terminalVersion;
        await using (var seed = Context(tenantId))
        {
            var service = Service(seed, tenantId);
            var created = await service.ScheduleAsync(request);
            if (terminalStatus == TaskStatuses.Cancelled)
            {
                await service.CancelAsync(created.Id);
            }
            else
            {
                var task = await seed.WorkItems.SingleAsync();
                task.Status = TaskStatuses.Completed;
                task.NextRunAtUtc = null;
                task.RunCount = 1;
                task.AttemptCount = 1;
                await seed.SaveChangesAsync();
            }
            terminalVersion = (await seed.WorkItems.SingleAsync()).RowVersion.ToArray();
        }
        await using (var replayContext = Context(tenantId))
        {
            var replay = await Service(replayContext, tenantId).ScheduleAsync(request);
            replay.Id.Should().Be(request.TaskId!.Value);
            replay.Status.Should().Be(terminalStatus);
            replay.NextRunAtUtc.Should().BeNull();
            replay.StartAtUtc.Should().Be(request.RunAtUtc);
            replay.RunCount.Should().Be(terminalStatus == TaskStatuses.Completed ? 1 : 0);
            replayContext.ChangeTracker.HasChanges().Should().BeFalse();
            await replayContext.SaveChangesAsync();
        }
        await using var verify = Context(tenantId);
        var saved = await verify.WorkItems.SingleAsync();
        saved.Status.Should().Be(terminalStatus);
        saved.NextRunAtUtc.Should().BeNull();
        saved.RowVersion.Should().Equal(terminalVersion);
    }

    [SkippableFact]
    public async Task StableId_Should_RejectChangedPayloadOrDueTime_WithoutChangingOriginalBinding()
    {
        RequireSqlServer();
        var tenantId = Guid.NewGuid();
        var request = Request();
        await using var db = Context(tenantId);
        var service = Service(db, tenantId);
        await service.ScheduleAsync(request);
        var original = await db.WorkItems.AsNoTracking().SingleAsync();

        Func<Task> payload = () => service.ScheduleAsync(request with { ActionPayloadJson = "{\"deliveryId\":\"another\"}" });
        await payload.Should().ThrowAsync<ArgumentException>();
        Func<Task> due = () => service.ScheduleAsync(request with { RunAtUtc = request.RunAtUtc!.Value.AddHours(1) });
        await due.Should().ThrowAsync<ArgumentException>();
        await db.SaveChangesAsync();

        await using var verify = Context(tenantId);
        var saved = await verify.WorkItems.SingleAsync();
        saved.ActionPayloadJson.Should().Be(original.ActionPayloadJson);
        saved.StartAtUtc.Should().Be(original.StartAtUtc);
        saved.NextRunAtUtc.Should().Be(original.NextRunAtUtc);
        saved.RowVersion.Should().Equal(original.RowVersion);
    }

    [SkippableFact]
    public async Task ForeignTenantPrimaryKeyCollision_Should_FailClosedWithoutReturningOrReplacingTask()
    {
        RequireSqlServer();
        var ownerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var request = Request();
        byte[] originalVersion;
        await using (var owner = Context(ownerTenant))
        {
            await Service(owner, ownerTenant).ScheduleAsync(request);
            originalVersion = (await owner.WorkItems.SingleAsync()).RowVersion.ToArray();
        }
        await using (var other = Context(otherTenant))
        {
            var service = Service(other, otherTenant);
            Func<Task> collision = () => service.ScheduleAsync(request);
            await collision.Should().ThrowAsync<DbUpdateException>();
            other.ChangeTracker.Entries<WorkItem>().Should().NotContain(x => x.State == EntityState.Added);
            await other.SaveChangesAsync();
            (await service.GetAsync(request.TaskId!.Value)).Should().BeNull();
            (await other.WorkItems.CountAsync()).Should().Be(0);
        }
        await using var verify = Context(ownerTenant);
        var original = await verify.WorkItems.SingleAsync();
        original.Id.Should().Be(request.TaskId!.Value);
        original.TenantId.Should().Be(ownerTenant);
        original.ActionPayloadJson.Should().Be(request.ActionPayloadJson);
        original.RowVersion.Should().Equal(originalVersion);
    }

    private void RequireSqlServer() => Skip.IfNot(database.IsAvailable, database.SkipReason ?? "SQL Server LocalDB unavailable.");

    private PlatformDbContext Context(Guid tenantId, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>(database.CreateOptions<PlatformDbContext>());
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, new TestTenantProvider(tenantId), new TestCurrentUserProvider(), new TestClock());
    }

    private static WorkItemService Service(PlatformDbContext db, Guid tenantId) => new(db,
        new TestTenantProvider(tenantId), new TestClock(), new RecurrenceCalculator(), new ActionCatalog());

    private static ScheduleTaskRequest Request() => new(
        Title: "Deliver purchased gift card", Kind: TaskKinds.ScheduledAction,
        ActionType: "commerce.send_gift_card", ActionPayloadJson: "{\"deliveryId\":\"saved-source\"}",
        AssigneeType: TaskAssigneeTypes.System, SubjectType: "GiftCardDelivery", SubjectId: Guid.NewGuid(),
        RunAtUtc: new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
        CorrelationId: Guid.NewGuid().ToString("N"), SourceModule: "Commerce", TaskId: Guid.NewGuid());

    private sealed class ActionCatalog : ITaskActionHandlerCatalog
    {
        public bool IsRegistered(string actionType) => actionType == "commerce.send_gift_card";
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class BeforeInsertBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = eventData.Context!;
            context.ChangeTracker.DetectChanges();
            if (context.ChangeTracker.Entries<WorkItem>().Any(x => x.State == EntityState.Added))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(25), cancellationToken);
            }
            return result;
        }
    }
}
