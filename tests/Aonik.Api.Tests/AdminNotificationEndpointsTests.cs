using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class AdminNotificationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AdminNotificationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetNotificationSummary_ShouldReturnUnreadCountForCurrentUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SeedNotificationsAsync(
            tenantId,
            new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "AgentTaskCompleted",
                Source = "Agent",
                Title = "Ready",
                Body = "The requested task has completed.",
                Severity = NotificationSeverities.Success,
                Status = NotificationStatuses.Unread,
                MetadataJson = "{}",
            },
            new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "AgentTaskCompleted",
                Source = "Agent",
                Title = "Seen",
                Body = "This one is already read.",
                Severity = NotificationSeverities.Info,
                Status = NotificationStatuses.Read,
                MetadataJson = "{}",
                ReadAt = DateTime.UtcNow,
            });

        var client = await CreateClientAsync(tenantId, userId);

        var response = await client.GetAsync("/admin/notifications/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<NotificationSummaryResponse>();
        payload.Should().NotBeNull();
        payload!.UnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task MarkNotificationRead_ShouldUpdateOwnedNotification()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var notification = new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Channel = NotificationChannels.InApp,
            Type = "ScheduledJobCommandQueued",
            Source = "Scheduler",
            Title = "Job queued",
            Body = "The background job command has been queued.",
            Severity = NotificationSeverities.Info,
            Status = NotificationStatuses.Unread,
            MetadataJson = "{}",
        };

        await SeedNotificationsAsync(tenantId, notification);
        var client = await CreateClientAsync(tenantId, userId);

        var response = await client.PostAsync($"/admin/notifications/{notification.Id}/read", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<NotificationResponse>();
        payload.Should().NotBeNull();
        payload!.Status.Should().Be(NotificationStatuses.Read);
        payload.ReadAt.Should().NotBeNull();

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.Notifications.SingleAsync(x => x.Id == notification.Id);
        persisted.Status.Should().Be(NotificationStatuses.Read);
        persisted.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task NotificationStream_ShouldEmitCreatedNotificationEvent()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var client = await CreateClientAsync(tenantId, userId);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/notifications/stream");
        request.Headers.Accept.ParseAdd("text/event-stream");

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        await notificationService.CreateForUserAsync(
            new CreateNotificationRequest(
                tenantId,
                userId,
                Type: "AgentTaskCompleted",
                Source: "Agent",
                Title: "Stream test",
                Body: "This notification should arrive over SSE.",
                Severity: NotificationSeverities.Success,
                ActionUrl: "/settings/background-jobs",
                CorrelationId: "stream-test",
                AiRunId: null,
                MetadataJson: "{}"));

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var helloPayload = await ReadNextDataPayloadAsync(reader, TimeSpan.FromSeconds(5));
        helloPayload.GetProperty("type").GetString().Should().Be("HELLO");

        var createdPayload = await ReadUntilAsync(
            reader,
            payload => payload.GetProperty("type").GetString() == "NOTIFICATION_CREATED",
            TimeSpan.FromSeconds(10));

        createdPayload.GetProperty("notification").GetProperty("title").GetString().Should().Be("Stream test");
        createdPayload.GetProperty("unreadCountDelta").GetInt32().Should().Be(1);
    }

    private async Task<HttpClient> CreateClientAsync(Guid tenantId, Guid userId)
    {
        var options = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("TenantAdmin");
        options.UserId = userId;

        return await _factory.CreateAuthenticatedClientAsync(options);
    }

    private async Task SeedNotificationsAsync(Guid tenantId, params Notification[] notifications)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.Notifications.RemoveRange(dbContext.Notifications);
        dbContext.Notifications.AddRange(notifications);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<JsonElement> ReadUntilAsync(
        StreamReader reader,
        Func<JsonElement, bool> predicate,
        TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            var payload = await ReadNextDataPayloadAsync(reader, timeout);
            if (predicate(payload))
            {
                return payload;
            }
        }

        throw new TimeoutException("Timed out waiting for notification stream payload.");
    }

    private static async Task<JsonElement> ReadNextDataPayloadAsync(StreamReader reader, TimeSpan timeout)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);

        while (!timeoutCts.IsCancellationRequested)
        {
            var readLineTask = reader.ReadLineAsync(timeoutCts.Token).AsTask();
            var completedTask = await Task.WhenAny(readLineTask, Task.Delay(timeout, timeoutCts.Token));

            if (completedTask != readLineTask)
            {
                throw new TimeoutException("Timed out waiting for notification stream line.");
            }

            var line = await readLineTask;
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line[6..]);
            return document.RootElement.Clone();
        }

        throw new TimeoutException("Timed out waiting for notification stream payload.");
    }
}
