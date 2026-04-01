using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Contracts.Services.Notifications;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Notifications;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Application.Tests.Notifications;

public class NotificationServiceTests
{
    [Fact]
    public async Task CreateForUserAsync_ShouldPersistUnreadNotification()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId, userId, clock);
        var service = CreateService(context, tenantId, userId, clock);

        var response = await service.CreateForUserAsync(
            new CreateNotificationRequest(
                tenantId,
                userId,
                Type: "AgentTaskCompleted",
                Source: "Agent",
                Title: "Invoice summary ready",
                Body: "The billing agent completed the requested invoice summary.",
                Severity: NotificationSeverities.Success,
                ActionUrl: "/billing/invoices",
                CorrelationId: "corr-1",
                AiRunId: null,
                MetadataJson: "{\"invoiceId\":\"inv-1\"}"));

        response.Status.Should().Be(NotificationStatuses.Unread);
        response.Channel.Should().Be(NotificationChannels.InApp);
        response.Source.Should().Be("Agent");

        var persisted = await context.Notifications.SingleAsync();
        persisted.Title.Should().Be("Invoice summary ready");
        persisted.MetadataJson.Should().Be("{\"invoiceId\":\"inv-1\"}");
        persisted.Status.Should().Be(NotificationStatuses.Unread);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_ShouldExcludeDismissedByDefault()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId, userId, clock);

        context.Notifications.AddRange(
            new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "InfrastructureAlert",
                Source = "AzureMonitor",
                Title = "CPU spike detected",
                Body = "Average CPU exceeded the alert threshold.",
                Severity = NotificationSeverities.Warning,
                Status = NotificationStatuses.Unread,
                MetadataJson = "{}",
                CreatedAt = clock.UtcNow.AddMinutes(-5),
            },
            new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "AgentAttentionRequired",
                Source = "Agent",
                Title = "Approval requested",
                Body = "Human approval is required to continue.",
                Severity = NotificationSeverities.Info,
                Status = NotificationStatuses.Dismissed,
                MetadataJson = "{}",
                DismissedAt = clock.UtcNow.AddMinutes(-10),
                CreatedAt = clock.UtcNow.AddMinutes(-10),
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId, clock);

        var results = await service.ListForCurrentUserAsync(new NotificationListRequest(Status: null));

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("CPU spike detected");
    }

    [Fact]
    public async Task MarkReadAsync_ShouldUpdateStatusAndSummary()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId, userId, clock);

        var notification = new Notification
        {
            TenantId = tenantId,
            UserId = userId,
            Channel = NotificationChannels.InApp,
            Type = "ScheduledJobCommandQueued",
            Source = "Scheduler",
            Title = "Job queued",
            Body = "The job command was queued successfully.",
            Severity = NotificationSeverities.Info,
            Status = NotificationStatuses.Unread,
            MetadataJson = "{}",
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddMinutes(2);
        var service = CreateService(context, tenantId, userId, clock);

        var updated = await service.MarkReadAsync(notification.Id);
        var summary = await service.GetSummaryForCurrentUserAsync();

        updated.Should().NotBeNull();
        updated!.Status.Should().Be(NotificationStatuses.Read);
        updated.ReadAt.Should().Be(clock.UtcNow);
        summary.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task MarkAllReadAsync_ShouldOnlyUpdateUnreadNotificationsForCurrentUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId, userId, clock);

        context.Notifications.AddRange(
            new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "AgentTaskCompleted",
                Source = "Agent",
                Title = "First",
                Body = "First notification",
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
                Title = "Second",
                Body = "Second notification",
                Severity = NotificationSeverities.Success,
                Status = NotificationStatuses.Unread,
                MetadataJson = "{}",
            },
            new Notification
            {
                TenantId = tenantId,
                UserId = otherUserId,
                Channel = NotificationChannels.InApp,
                Type = "AgentAttentionRequired",
                Source = "Agent",
                Title = "Other user",
                Body = "Should remain unread",
                Severity = NotificationSeverities.Warning,
                Status = NotificationStatuses.Unread,
                MetadataJson = "{}",
            });

        await context.SaveChangesAsync();

        clock.UtcNow = clock.UtcNow.AddMinutes(5);
        var service = CreateService(context, tenantId, userId, clock);

        var result = await service.MarkAllReadAsync();

        result.AffectedCount.Should().Be(2);

        var userNotifications = await context.Notifications
            .Where(x => x.UserId == userId)
            .ToListAsync();
        userNotifications.Should().OnlyContain(x => x.Status == NotificationStatuses.Read);
        userNotifications.Should().OnlyContain(x => x.ReadAt == clock.UtcNow);

        var otherNotification = await context.Notifications.SingleAsync(x => x.UserId == otherUserId);
        otherNotification.Status.Should().Be(NotificationStatuses.Unread);
    }

    [Fact]
    public async Task ListForCurrentUserAsync_ShouldIncludeGlobalNotificationsForCurrentUser()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(tenantId, userId, clock);

        context.Notifications.AddRange(
            new Notification
            {
                TenantId = tenantId,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "ScheduledJobCommandQueued",
                Source = "Scheduler",
                Title = "Tenant scoped",
                Body = "Tenant scoped notification",
                Severity = NotificationSeverities.Info,
                Status = NotificationStatuses.Unread,
                MetadataJson = "{}",
                CreatedAt = clock.UtcNow,
            },
            new Notification
            {
                TenantId = Guid.Empty,
                UserId = userId,
                Channel = NotificationChannels.InApp,
                Type = "PlatformPerformanceResolved",
                Source = "AzureMonitor",
                Title = "Global alert",
                Body = "Global platform alert notification",
                Severity = NotificationSeverities.Success,
                Status = NotificationStatuses.Unread,
                MetadataJson = "{}",
                CreatedAt = clock.UtcNow.AddMinutes(-1),
            });

        await context.SaveChangesAsync();

        var service = CreateService(context, tenantId, userId, clock);

        var results = await service.ListForCurrentUserAsync(new NotificationListRequest(Status: null));
        var summary = await service.GetSummaryForCurrentUserAsync();

        results.Should().HaveCount(2);
        results.Select(x => x.Title).Should().Contain(new[] { "Tenant scoped", "Global alert" });
        summary.UnreadCount.Should().Be(2);
    }

    private static NotificationService CreateService(
        PlatformDbContext context,
        Guid tenantId,
        Guid userId,
        IClock clock)
    {
        return new NotificationService(
            context,
            new TestTenantContext(tenantId),
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            new NotificationRealtimePublisher(),
            new TestPushNotificationSender(),
            clock);
    }

    private static PlatformDbContext CreateDbContext(Guid tenantId, Guid userId, IClock clock)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"NotificationService_{Guid.NewGuid()}")
            .Options;

        return new PlatformDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            clock);
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId)
        {
            _tenantId = tenantId;
        }

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(Guid tenantId)
        {
            TenantId = tenantId;
            ResolutionSource = "Test";
        }

        public Guid? TenantId { get; set; }

        public string? ResolutionSource { get; set; }

        public bool IsResolved => TenantId.HasValue;
    }

    private sealed class TestCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public TestCurrentUserProvider(Guid userId)
        {
            _userId = userId;
        }

        public Guid? GetCurrentUserId() => _userId;

        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = _userId;
            return true;
        }
    }

    private sealed class TestClock : IClock
    {
        public TestClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }

    private sealed class TestPushNotificationSender : IPushNotificationSender
    {
        public Task<PushNotificationDispatchResult> SendAsync(
            PushNotificationDispatchRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PushNotificationDispatchResult(Array.Empty<Guid>()));
        }
    }
}
