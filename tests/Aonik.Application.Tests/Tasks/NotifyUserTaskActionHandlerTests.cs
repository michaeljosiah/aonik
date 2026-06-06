using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Services.Tasks;
using Aonik.SharedKernel.Abstractions.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace Aonik.Application.Tests.Tasks;

public sealed class NotifyUserTaskActionHandlerTests
{
    private static NotificationResponse Echo(CreateNotificationRequest r) => new(
        Id: Guid.NewGuid(),
        TenantId: r.TenantId,
        UserId: r.UserId,
        Channel: r.Channel,
        Type: r.Type,
        Source: r.Source,
        Title: r.Title,
        Body: r.Body,
        Severity: r.Severity,
        Status: "Unread",
        ActionUrl: r.ActionUrl,
        CorrelationId: r.CorrelationId,
        AiRunId: r.AiRunId,
        MetadataJson: r.MetadataJson ?? "{}",
        CreatedAt: new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc),
        ReadAt: null,
        DismissedAt: null);

    private static (NotifyUserTaskActionHandler Handler, Mock<INotificationService> Service) CreateHandler()
    {
        var service = new Mock<INotificationService>();
        service
            .Setup(s => s.CreateForUserAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CreateNotificationRequest r, CancellationToken _) => Echo(r));
        return (new NotifyUserTaskActionHandler(service.Object), service);
    }

    private static TaskActionContext Context(string payloadJson, Guid? assigneeId = null) => new(
        TenantId: Guid.NewGuid(),
        WorkItemId: Guid.NewGuid(),
        RunId: Guid.NewGuid(),
        Kind: TaskKinds.Reminder,
        SubjectType: "Bill",
        SubjectId: Guid.NewGuid(),
        AssigneeType: TaskAssigneeTypes.System,
        AssigneeId: assigneeId,
        AssigneeKey: null,
        ScheduledForUtc: new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc),
        ActionPayloadJson: payloadJson);

    [Fact]
    public void ActionType_Should_Be_NotifyUser()
    {
        var (handler, _) = CreateHandler();
        handler.ActionType.Should().Be(TaskActionTypes.NotifyUser);
    }

    [Fact]
    public async Task ExecuteAsync_Should_PostNotification_And_ReturnSucceeded()
    {
        var (handler, service) = CreateHandler();
        var userId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new
        {
            userId,
            severity = "Warning",
            title = "Insurance renewal coming up",
            body = "Your policy is due on 2026-06-20.",
        });
        var context = Context(payload);

        var result = await handler.ExecuteAsync(context);

        result.Outcome.Should().Be(TaskActionOutcome.Succeeded);
        service.Verify(s => s.CreateForUserAsync(
            It.Is<CreateNotificationRequest>(r =>
                r.TenantId == context.TenantId
                && r.UserId == userId
                && r.Title == "Insurance renewal coming up"
                && r.Severity == NotificationSeverities.Warning
                && r.CorrelationId == context.WorkItemId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_FallBackToAssignee_When_PayloadUserIdMissing()
    {
        var (handler, service) = CreateHandler();
        var assignee = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { title = "Hi", body = "There" });

        var result = await handler.ExecuteAsync(Context(payload, assigneeId: assignee));

        result.Outcome.Should().Be(TaskActionOutcome.Succeeded);
        service.Verify(s => s.CreateForUserAsync(
            It.Is<CreateNotificationRequest>(r => r.UserId == assignee), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_NormalizeLowercaseSeverity()
    {
        var (handler, service) = CreateHandler();
        var payload = JsonSerializer.Serialize(new { userId = Guid.NewGuid(), severity = "warning", title = "t", body = "b" });

        await handler.ExecuteAsync(Context(payload));

        service.Verify(s => s.CreateForUserAsync(
            It.Is<CreateNotificationRequest>(r => r.Severity == NotificationSeverities.Warning), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_DefaultSeverity_When_Unknown()
    {
        var (handler, service) = CreateHandler();
        var payload = JsonSerializer.Serialize(new { userId = Guid.NewGuid(), severity = "Nonsense", title = "t", body = "b" });

        await handler.ExecuteAsync(Context(payload));

        service.Verify(s => s.CreateForUserAsync(
            It.Is<CreateNotificationRequest>(r => r.Severity == NotificationSeverities.Info), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReturnFailed_When_PayloadInvalid()
    {
        var (handler, service) = CreateHandler();

        var result = await handler.ExecuteAsync(Context("{ not valid json"));

        result.Outcome.Should().Be(TaskActionOutcome.Failed);
        result.Error.Should().Contain("payload");
        service.Verify(s => s.CreateForUserAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ReturnFailed_When_NoTargetUser()
    {
        var (handler, service) = CreateHandler();
        var payload = JsonSerializer.Serialize(new { title = "t", body = "b" }); // no userId, no assignee

        var result = await handler.ExecuteAsync(Context(payload));

        result.Outcome.Should().Be(TaskActionOutcome.Failed);
        service.Verify(s => s.CreateForUserAsync(It.IsAny<CreateNotificationRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
