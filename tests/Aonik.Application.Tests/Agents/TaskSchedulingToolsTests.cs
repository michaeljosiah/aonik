using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Agents.Tools;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.TestSupport.Identity;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 034 — the AI-chat reminder tools (create_reminder / list_reminders / cancel_reminder).
/// They route through the cross-module <see cref="ITaskService"/>, always target the current user,
/// and never accept an arbitrary userId.
/// </summary>
public sealed class TaskSchedulingToolsTests
{
    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; set; } = new(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc);
    }

    private static TaskResponse Echo(ScheduleTaskRequest r) => new(
        Guid.NewGuid(), Guid.NewGuid(), r.Title, r.Description, r.Kind, r.SubjectType, r.SubjectId,
        r.AssigneeType, r.AssigneeId, r.AssigneeKey, r.ActionType,
        string.IsNullOrEmpty(r.RecurrenceCron) ? "OneOff" : "Recurring",
        r.RunAtUtc, r.RecurrenceCron, r.Timezone, r.StartAtUtc, r.EndAtUtc, 0, r.MaxRuns,
        "Scheduled", r.Priority, r.SourceModule ?? "Agent", r.CorrelationId, null,
        new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc), null);

    private static TaskResponse Existing(Guid id, string assigneeType, Guid? assigneeId) => new(
        id, Guid.NewGuid(), "Reminder", null, "Reminder", null, null, assigneeType, assigneeId, null,
        "notify_user", "OneOff", null, null, null, null, null, 0, null, "Scheduled", 0, "Agent", null, null,
        new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc), null);

    private static (ServiceProvider Sp, Mock<ITaskService> Svc, Guid UserId) Build()
    {
        var userId = Guid.NewGuid();
        var svc = new Mock<ITaskService>();
        svc.Setup(s => s.ScheduleAsync(It.IsAny<ScheduleTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleTaskRequest r, CancellationToken _) => Echo(r));

        var services = new ServiceCollection();
        services.AddSingleton(svc.Object);
        services.AddSingleton<ICurrentUserProvider>(new TestCurrentUserProvider(userId));
        services.AddSingleton<IClock>(new FixedClock());
        return (services.BuildServiceProvider(), svc, userId);
    }

    private static AIFunction Tool(IServiceProvider sp, string name) =>
        TaskSchedulingTools.CreateAll(sp).Cast<AIFunction>().Single(f => f.Name == name);

    [Fact]
    public async Task CreateReminder_Should_ScheduleNotifyUser_ForCurrentUser_Using_InMinutes()
    {
        var (sp, svc, userId) = Build();

        await Tool(sp, "create_reminder").InvokeAsync(new AIFunctionArguments
        {
            ["title"] = "Call mum",
            ["body"] = "Ring her",
            ["inMinutes"] = 30,
        });

        svc.Verify(s => s.ScheduleAsync(It.Is<ScheduleTaskRequest>(r =>
                r.ActionType == TaskActionTypes.NotifyUser
                && r.Kind == TaskKinds.Reminder
                && r.AssigneeType == TaskAssigneeTypes.User
                && r.AssigneeId == userId
                && r.RunAtUtc == new DateTime(2026, 6, 6, 10, 30, 0, DateTimeKind.Utc)
                && r.RecurrenceCron == null
                && r.ActionPayloadJson.Contains("Ring her")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateReminder_Should_PassCron_For_RecurringReminder()
    {
        var (sp, svc, _) = Build();

        await Tool(sp, "create_reminder").InvokeAsync(new AIFunctionArguments
        {
            ["title"] = "Standup",
            ["recurrenceCron"] = "0 0 9 * * ?",
        });

        svc.Verify(s => s.ScheduleAsync(It.Is<ScheduleTaskRequest>(r =>
                r.RecurrenceCron == "0 0 9 * * ?" && r.RunAtUtc == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListReminders_Should_ScopeToCurrentUser()
    {
        var (sp, svc, userId) = Build();
        svc.Setup(s => s.ListForAssigneeAsync(TaskAssigneeTypes.User, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Existing(Guid.NewGuid(), TaskAssigneeTypes.User, userId) });

        await Tool(sp, "list_reminders").InvokeAsync(new AIFunctionArguments());

        svc.Verify(s => s.ListForAssigneeAsync(TaskAssigneeTypes.User, userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelReminder_Should_NotCancel_When_TaskBelongsToAnotherUser()
    {
        var (sp, svc, _) = Build();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Existing(id, TaskAssigneeTypes.User, Guid.NewGuid())); // different user

        await Tool(sp, "cancel_reminder").InvokeAsync(new AIFunctionArguments { ["reminderId"] = id });

        svc.Verify(s => s.CancelAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelReminder_Should_Cancel_When_OwnedByCurrentUser()
    {
        var (sp, svc, userId) = Build();
        var id = Guid.NewGuid();
        svc.Setup(s => s.GetAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Existing(id, TaskAssigneeTypes.User, userId));

        await Tool(sp, "cancel_reminder").InvokeAsync(new AIFunctionArguments { ["reminderId"] = id });

        svc.Verify(s => s.CancelAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }
}
