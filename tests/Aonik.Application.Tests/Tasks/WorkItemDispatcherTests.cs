using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Platform.Contracts.Services.Tasks;
using Aonik.Platform.Entities.Tasks;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Tasks;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Persistence;
using Aonik.TestSupport.Identity;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Aonik.Application.Tests.Tasks;

public sealed class WorkItemDispatcherTests
{
    private const string TestAction = "test_action";

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class MutableTenantContext : ITenantContext
    {
        public Guid? TenantId { get; set; }
        public string? ResolutionSource { get; set; }
        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class ContextTenantProvider : ITenantProvider
    {
        private readonly ITenantContext _context;
        public ContextTenantProvider(ITenantContext context) => _context = context;
        public Guid GetCurrentTenantId() => _context.TenantId ?? throw new InvalidOperationException("No tenant.");
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _context.TenantId ?? Guid.Empty;
            return _context.TenantId.HasValue;
        }
    }

    private sealed class StubHandler : ITaskActionHandler
    {
        private readonly Func<TaskActionContext, TaskActionResult> _impl;
        private int _invocations;

        public StubHandler(string actionType, Func<TaskActionContext, TaskActionResult> impl)
        {
            ActionType = actionType;
            _impl = impl;
        }

        public string ActionType { get; }
        public int Invocations => Volatile.Read(ref _invocations);

        public Task<TaskActionResult> ExecuteAsync(TaskActionContext context, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _invocations);
            return Task.FromResult(_impl(context));
        }
    }

    private static ServiceProvider BuildProvider(IClock clock, params ITaskActionHandler[] handlers)
    {
        var services = new ServiceCollection();
        var dbName = $"WorkItemDispatcher_{Guid.NewGuid()}";

        services.AddScoped<ITenantContext, MutableTenantContext>();
        services.AddScoped<ITenantProvider, ContextTenantProvider>();
        services.AddSingleton<ICurrentUserProvider>(new TestCurrentUserProvider(Guid.NewGuid()));
        services.AddSingleton(clock);
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton<RecurrenceCalculator>();
        services.AddLogging();
        services.AddScoped<IWorkItemDispatcher, WorkItemDispatcher>();

        foreach (var handler in handlers)
        {
            services.AddKeyedSingleton<ITaskActionHandler>(handler.ActionType, handler);
        }

        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SeedAsync(IServiceProvider root, Guid tenantId, Action<WorkItem> configure)
    {
        using var scope = root.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = tenantId;
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Title = "Test task",
            Kind = TaskKinds.Reminder,
            ActionType = TestAction,
            ActionPayloadJson = "{}",
            AssigneeType = TaskAssigneeTypes.System,
            ScheduleType = TaskScheduleTypes.OneOff,
            Status = TaskStatuses.Scheduled,
        };
        configure(workItem);

        db.WorkItems.Add(workItem);
        await db.SaveChangesAsync();
        return workItem.Id;
    }

    private static async Task<WorkItem?> GetWorkItemAsync(IServiceProvider root, Guid id)
    {
        using var scope = root.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = null;
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.WorkItems.AcrossTenants().AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);
    }

    private static async Task<List<WorkItemRun>> GetRunsAsync(IServiceProvider root, Guid workItemId)
    {
        using var scope = root.CreateScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().TenantId = null;
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.WorkItemRuns.AcrossTenants().AsNoTracking()
            .Where(r => r.WorkItemId == workItemId).ToListAsync();
    }

    private static async Task<WorkItemDispatchSummary> DispatchAsync(
        IServiceProvider root, WorkItemDispatchOptions? options = null)
    {
        using var scope = root.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IWorkItemDispatcher>();
        return await dispatcher.DispatchDueAsync(options ?? new WorkItemDispatchOptions(MaxAttempts: 3));
    }

    [Fact]
    public async Task DispatchDueAsync_Should_RunHandler_And_CompleteOneOff_When_Due()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded, ResultJson: "{\"ok\":true}"));
        var root = BuildProvider(clock, handler);
        var tenant = Guid.NewGuid();
        var id = await SeedAsync(root, tenant, w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1));

        var summary = await DispatchAsync(root);

        summary.Succeeded.Should().Be(1);
        handler.Invocations.Should().Be(1);

        var workItem = await GetWorkItemAsync(root, id);
        workItem!.Status.Should().Be(TaskStatuses.Completed);
        workItem.NextRunAtUtc.Should().BeNull();
        workItem.RunCount.Should().Be(1);
        workItem.LeasedUntilUtc.Should().BeNull();

        var runs = await GetRunsAsync(root, id);
        runs.Should().ContainSingle();
        runs[0].Outcome.Should().Be(nameof(TaskActionOutcome.Succeeded));
        runs[0].CompletedAtUtc.Should().NotBeNull();
        runs[0].TenantId.Should().Be(tenant);
    }

    [Fact]
    public async Task DispatchDueAsync_Should_NotRun_When_NotYetDue()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        var id = await SeedAsync(root, Guid.NewGuid(), w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(10));

        var summary = await DispatchAsync(root);

        summary.Considered.Should().Be(0);
        handler.Invocations.Should().Be(0);
        (await GetWorkItemAsync(root, id))!.Status.Should().Be(TaskStatuses.Scheduled);
    }

    [Fact]
    public async Task DispatchDueAsync_Should_BeIdempotent_When_DispatchedTwice()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        var id = await SeedAsync(root, Guid.NewGuid(), w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1));

        await DispatchAsync(root);
        await DispatchAsync(root);

        handler.Invocations.Should().Be(1);
        (await GetRunsAsync(root, id)).Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchDueAsync_Should_ReArmRecurring_To_NextOccurrence()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        var id = await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.ScheduleType = TaskScheduleTypes.Recurring;
            w.RecurrenceCron = "0 * * * * ?";
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1);
        });

        await DispatchAsync(root);

        var workItem = await GetWorkItemAsync(root, id);
        workItem!.Status.Should().Be(TaskStatuses.Scheduled);
        workItem.NextRunAtUtc.Should().BeAfter(clock.UtcNow);
        workItem.RunCount.Should().Be(1);
        (await GetRunsAsync(root, id)).Should().ContainSingle();
    }

    [Fact]
    public async Task DispatchDueAsync_Should_RecordProposal_When_HandlerProposes()
    {
        var clock = new TestClock();
        var proposalId = Guid.NewGuid();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Proposed, ProposalId: proposalId));
        var root = BuildProvider(clock, handler);
        var id = await SeedAsync(root, Guid.NewGuid(), w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1));

        var summary = await DispatchAsync(root);

        summary.Proposed.Should().Be(1);
        var runs = await GetRunsAsync(root, id);
        runs[0].Outcome.Should().Be(nameof(TaskActionOutcome.Proposed));
        runs[0].ProposalId.Should().Be(proposalId);
        (await GetWorkItemAsync(root, id))!.Status.Should().Be(TaskStatuses.Completed);
    }

    [Fact]
    public async Task DispatchDueAsync_Should_RetrySameOccurrence_Then_Fail_When_AttemptsExhausted()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Failed, Error: "boom"));
        var root = BuildProvider(clock, handler);
        var options = new WorkItemDispatchOptions(MaxAttempts: 3);
        var id = await SeedAsync(root, Guid.NewGuid(), w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1));

        await DispatchAsync(root, options);
        var afterFirst = await GetWorkItemAsync(root, id);
        afterFirst!.AttemptCount.Should().Be(1);
        afterFirst.Status.Should().Be(TaskStatuses.Scheduled, "the occurrence is retried while attempts remain");
        afterFirst.NextRunAtUtc.Should().NotBeNull();
        (await GetRunsAsync(root, id)).Should().ContainSingle("the in-flight run is reused across retries");

        await DispatchAsync(root, options);
        await DispatchAsync(root, options);

        var afterExhausted = await GetWorkItemAsync(root, id);
        afterExhausted!.Status.Should().Be(TaskStatuses.Failed);
        afterExhausted.AttemptCount.Should().Be(3);
        handler.Invocations.Should().Be(3);
        var runs = await GetRunsAsync(root, id);
        runs.Should().ContainSingle();
        runs[0].Outcome.Should().Be(nameof(TaskActionOutcome.Failed));
        runs[0].Error.Should().Be("boom");
    }

    [Fact]
    public async Task DispatchDueAsync_Should_FailTerminally_When_NoHandlerRegistered()
    {
        var clock = new TestClock();
        var root = BuildProvider(clock); // no handlers registered
        var id = await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.ActionType = "unregistered_action";
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1);
        });

        var summary = await DispatchAsync(root, new WorkItemDispatchOptions(MaxAttempts: 1));

        summary.Failed.Should().Be(1);
        var workItem = await GetWorkItemAsync(root, id);
        workItem!.Status.Should().Be(TaskStatuses.Failed);
        workItem.LastError.Should().Contain("handler");
        (await GetRunsAsync(root, id))[0].Outcome.Should().Be(nameof(TaskActionOutcome.Failed));
    }

    [Fact]
    public async Task DispatchDueAsync_Should_SkipLeasedItem_With_UnexpiredLease()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1);
            w.LeasedUntilUtc = clock.UtcNow.AddMinutes(5);
            w.LeasedBy = "another-worker";
        });

        var summary = await DispatchAsync(root);

        summary.Considered.Should().Be(0);
        handler.Invocations.Should().Be(0);
    }

    [Fact]
    public async Task DispatchDueAsync_Should_ProcessAcrossTenants()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        await SeedAsync(root, Guid.NewGuid(), w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1));
        await SeedAsync(root, Guid.NewGuid(), w => w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1));

        var summary = await DispatchAsync(root);

        summary.Succeeded.Should().Be(2);
        handler.Invocations.Should().Be(2);
    }

    [Fact]
    public async Task DispatchDueAsync_Should_Complete_When_MaxRunsReached()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        var id = await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.ScheduleType = TaskScheduleTypes.Recurring;
            w.RecurrenceCron = "0 * * * * ?";
            w.MaxRuns = 1;
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1);
        });

        await DispatchAsync(root);

        var workItem = await GetWorkItemAsync(root, id);
        workItem!.RunCount.Should().Be(1);
        workItem.Status.Should().Be(TaskStatuses.Completed);
        workItem.NextRunAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueAsync_Should_ReclaimInProgressItem_When_LeaseExpired()
    {
        // Simulates a worker that claimed an item (Status=InProgress + lease) then crashed
        // mid-execution; the lease has since lapsed. The next sweep must reclaim and run it.
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        var id = await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.Status = TaskStatuses.InProgress;
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-5);
            w.LeasedBy = "crashed-worker";
            w.LeasedUntilUtc = clock.UtcNow.AddMinutes(-1); // expired
        });

        var summary = await DispatchAsync(root);

        summary.Succeeded.Should().Be(1);
        handler.Invocations.Should().Be(1);
        var workItem = await GetWorkItemAsync(root, id);
        workItem!.Status.Should().Be(TaskStatuses.Completed);
        workItem.LeasedUntilUtc.Should().BeNull();
    }

    [Fact]
    public async Task DispatchDueAsync_Should_NotReclaimInProgressItem_When_LeaseStillActive()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Succeeded));
        var root = BuildProvider(clock, handler);
        await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.Status = TaskStatuses.InProgress;
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-5);
            w.LeasedUntilUtc = clock.UtcNow.AddMinutes(5); // still held
        });

        var summary = await DispatchAsync(root);

        summary.Considered.Should().Be(0);
        handler.Invocations.Should().Be(0);
    }

    [Fact]
    public async Task DispatchDueAsync_Should_StopFailingRecurringTask_When_MaxRunsReached()
    {
        var clock = new TestClock();
        var handler = new StubHandler(TestAction, _ => new TaskActionResult(TaskActionOutcome.Failed, Error: "down"));
        var root = BuildProvider(clock, handler);
        var options = new WorkItemDispatchOptions(MaxAttempts: 1); // each occurrence exhausts in one attempt
        var id = await SeedAsync(root, Guid.NewGuid(), w =>
        {
            w.ScheduleType = TaskScheduleTypes.Recurring;
            w.RecurrenceCron = "0 * * * * ?";
            w.MaxRuns = 2;
            w.NextRunAtUtc = clock.UtcNow.AddMinutes(-1);
        });

        await DispatchAsync(root, options); // occurrence 1 fails → counts toward MaxRuns, re-armed
        var afterOne = await GetWorkItemAsync(root, id);
        afterOne!.RunCount.Should().Be(1);
        afterOne.Status.Should().Be(TaskStatuses.Scheduled);

        clock.UtcNow = clock.UtcNow.AddMinutes(2); // advance past the re-armed occurrence
        await DispatchAsync(root, options); // occurrence 2 fails → RunCount hits MaxRuns → stop

        var afterTwo = await GetWorkItemAsync(root, id);
        afterTwo!.RunCount.Should().Be(2);
        afterTwo.Status.Should().Be(TaskStatuses.Completed);
        afterTwo.NextRunAtUtc.Should().BeNull();
    }
}
