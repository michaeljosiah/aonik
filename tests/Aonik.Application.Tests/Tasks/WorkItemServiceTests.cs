using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Tasks;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.TestSupport.Identity;
using Aonik.TestSupport.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Aonik.Application.Tests.Tasks;

public sealed class WorkItemServiceTests
{
    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);
    }

    private sealed class FakeCatalog : ITaskActionHandlerCatalog
    {
        private readonly HashSet<string> _known;
        public FakeCatalog(params string[] known) => _known = new HashSet<string>(known, StringComparer.Ordinal);
        public bool IsRegistered(string actionType) => _known.Contains(actionType);
    }

    private static (WorkItemService Service, PlatformDbContext Db) CreateService(
        Guid tenantId, IClock clock, ITaskActionHandlerCatalog catalog)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"WorkItemService_{Guid.NewGuid()}")
            .Options;
        var tenantProvider = new TestTenantProvider(tenantId);
        var db = new PlatformDbContext(options, tenantProvider, new TestCurrentUserProvider(Guid.NewGuid()), clock);
        var service = new WorkItemService(db, tenantProvider, clock, new RecurrenceCalculator(), catalog);
        return (service, db);
    }

    private static ScheduleTaskRequest NotifyRequest(DateTime? runAt = null, string? cron = null) => new(
        Title: "Insurance due",
        Kind: TaskKinds.Reminder,
        ActionType: TaskActionTypes.NotifyUser,
        ActionPayloadJson: "{}",
        AssigneeType: TaskAssigneeTypes.System,
        RunAtUtc: runAt,
        RecurrenceCron: cron);

    [Fact]
    public async Task ScheduleAsync_Should_CreateOneOffTask_When_RunAtProvided()
    {
        var tenant = Guid.NewGuid();
        var clock = new TestClock();
        var (service, _) = CreateService(tenant, clock, new FakeCatalog(TaskActionTypes.NotifyUser));
        var runAt = clock.UtcNow.AddDays(1);

        var result = await service.ScheduleAsync(NotifyRequest(runAt: runAt));

        result.ScheduleType.Should().Be(TaskScheduleTypes.OneOff);
        result.Status.Should().Be(TaskStatuses.Scheduled);
        result.NextRunAtUtc.Should().Be(runAt);
        result.TenantId.Should().Be(tenant);
    }

    [Fact]
    public async Task ScheduleAsync_Should_Reject_When_ActionTypeNotRegistered()
    {
        var (service, _) = CreateService(Guid.NewGuid(), new TestClock(), new FakeCatalog());

        var act = () => service.ScheduleAsync(NotifyRequest(runAt: DateTime.UtcNow));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*notify_user*");
    }

    [Fact]
    public async Task ScheduleAsync_Should_ComputeFirstOccurrence_When_RecurringCron()
    {
        var clock = new TestClock(); // 2026-06-06 10:00:00 UTC
        var (service, _) = CreateService(Guid.NewGuid(), clock, new FakeCatalog(TaskActionTypes.NotifyUser));

        var result = await service.ScheduleAsync(NotifyRequest(cron: "0 * * * * ?"));

        result.ScheduleType.Should().Be(TaskScheduleTypes.Recurring);
        result.Status.Should().Be(TaskStatuses.Scheduled);
        result.NextRunAtUtc.Should().Be(new DateTime(2026, 6, 6, 10, 1, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ScheduleAsync_Should_Throw_When_BothRunAtAndCronProvided()
    {
        var (service, _) = CreateService(Guid.NewGuid(), new TestClock(), new FakeCatalog(TaskActionTypes.NotifyUser));

        var act = () => service.ScheduleAsync(NotifyRequest(runAt: DateTime.UtcNow, cron: "0 * * * * ?"));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ScheduleAsync_Should_Throw_When_CronInvalid()
    {
        var (service, _) = CreateService(Guid.NewGuid(), new TestClock(), new FakeCatalog(TaskActionTypes.NotifyUser));

        var act = () => service.ScheduleAsync(NotifyRequest(cron: "not a cron"));

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*cron*");
    }

    [Fact]
    public async Task ScheduleAsync_Should_Throw_When_TitleMissing()
    {
        var (service, _) = CreateService(Guid.NewGuid(), new TestClock(), new FakeCatalog(TaskActionTypes.NotifyUser));

        var act = () => service.ScheduleAsync(NotifyRequest(runAt: DateTime.UtcNow) with { Title = "  " });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_When_NotFound()
    {
        var (service, _) = CreateService(Guid.NewGuid(), new TestClock(), new FakeCatalog(TaskActionTypes.NotifyUser));

        (await service.GetAsync(Guid.NewGuid())).Should().BeNull();
    }

    [Fact]
    public async Task CancelAsync_Should_SetCancelled_And_ClearNextRun()
    {
        var clock = new TestClock();
        var (service, _) = CreateService(Guid.NewGuid(), clock, new FakeCatalog(TaskActionTypes.NotifyUser));
        var created = await service.ScheduleAsync(NotifyRequest(runAt: clock.UtcNow.AddHours(1)));

        await service.CancelAsync(created.Id);

        var fetched = await service.GetAsync(created.Id);
        fetched!.Status.Should().Be(TaskStatuses.Cancelled);
        fetched.NextRunAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task PauseAsync_Then_ResumeAsync_Should_ToggleStatus()
    {
        var clock = new TestClock();
        var (service, _) = CreateService(Guid.NewGuid(), clock, new FakeCatalog(TaskActionTypes.NotifyUser));
        var created = await service.ScheduleAsync(NotifyRequest(runAt: clock.UtcNow.AddHours(1)));

        await service.PauseAsync(created.Id);
        (await service.GetAsync(created.Id))!.Status.Should().Be(TaskStatuses.Paused);

        await service.ResumeAsync(created.Id);
        (await service.GetAsync(created.Id))!.Status.Should().Be(TaskStatuses.Scheduled);
    }

    [Fact]
    public async Task CancelAsync_Should_Throw_When_NotFound()
    {
        var (service, _) = CreateService(Guid.NewGuid(), new TestClock(), new FakeCatalog(TaskActionTypes.NotifyUser));

        var act = () => service.CancelAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ListForSubjectAsync_Should_ReturnMatchingOnly()
    {
        var clock = new TestClock();
        var (service, _) = CreateService(Guid.NewGuid(), clock, new FakeCatalog(TaskActionTypes.NotifyUser));
        var billId = Guid.NewGuid();
        await service.ScheduleAsync(NotifyRequest(runAt: clock.UtcNow.AddHours(1)) with { SubjectType = "Bill", SubjectId = billId });
        await service.ScheduleAsync(NotifyRequest(runAt: clock.UtcNow.AddHours(1)) with { SubjectType = "Order", SubjectId = Guid.NewGuid() });

        var forBill = await service.ListForSubjectAsync("Bill", billId);

        forBill.Should().ContainSingle();
        forBill[0].SubjectId.Should().Be(billId);
    }
}
