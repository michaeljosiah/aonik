using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Contracts.Models.Notifications;
using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Entities.Notifications;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Notifications;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Notifications;
using Aonik.Platform.Services.Operations;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Operations;

public class AzureMonitorAlertServicesTests
{
    [Fact]
    public async Task IngestAzureMonitorAlertAsync_ShouldPersistAlertAndEnqueueProcessing()
    {
        using var context = CreateDbContext(Guid.Empty, Guid.NewGuid(), new TestClock(DateTime.UtcNow));
        var queue = new TestAlertProcessingQueue();
        var service = new AlertIngestionService(
            context,
            queue,
            new TestClock(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc)),
            NullLogger<AlertIngestionService>.Instance);

        var response = await service.IngestAzureMonitorAlertAsync(CreateWebhookRequest("Fired"));

        response.Status.Should().Be(AzureMonitorAlertStatuses.Received);
        queue.EnqueuedAlertIds.Should().HaveCount(1);
        queue.EnqueuedAlertIds[0].Should().Be(response.AlertId);

        var persisted = await context.AzureMonitorAlertEvents.SingleAsync();
        persisted.TenantId.Should().Be(Guid.Empty);
        persisted.Provider.Should().Be(AzureMonitorAlertProviders.AzureMonitor);
        persisted.AlertRuleName.Should().Be("prod-api-5xx-spike");
        persisted.NormalizedType.Should().Be(AzureMonitorAlertTypes.PlatformPerformanceAlert);
    }

    [Fact]
    public async Task ProcessAsync_ShouldCreateGlobalResolvedNotificationForPlatformAdmins()
    {
        var tenantId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var platformAdminUserId = Guid.NewGuid();
        var clock = new TestClock(new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc));
        using var context = CreateDbContext(Guid.Empty, currentUserId, clock);

        var user = new User
        {
            Id = platformAdminUserId,
            TenantId = tenantId,
            ExternalIssuer = "test",
            ExternalSubject = "platform-admin",
            Email = "platform-admin@example.com",
            Status = "Active",
        };

        var role = new Role
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.Empty,
            Name = "PlatformAdmin",
        };

        context.Users.Add(user);
        context.Roles.Add(role);
        context.UserRoles.Add(new UserRole
        {
            UserId = user.Id,
            User = user,
            RoleId = role.Id,
            Role = role,
        });

        var alertEvent = new AzureMonitorAlertEvent
        {
            TenantId = Guid.Empty,
            Provider = AzureMonitorAlertProviders.AzureMonitor,
            ExternalAlertId = "alert-1",
            AlertRuleName = "prod-api-5xx-spike",
            AlertRuleId = "alert-1",
            MonitorCondition = AzureMonitorAlertConditions.Resolved,
            Severity = "Sev2",
            SignalType = "Log",
            MonitoringService = "Application Insights",
            NormalizedType = AzureMonitorAlertTypes.PlatformPerformanceResolved,
            CorrelationKey = "corr-1",
            Status = AzureMonitorAlertStatuses.Received,
            ResourceIdsJson = "[\"/subscriptions/test/resourceGroups/rg/providers/Microsoft.App/containerApps/api\"]",
            EssentialsJson = "{}",
            AlertContextJson = "{}",
            CustomPropertiesJson = "{}",
            AnalysisSummary = string.Empty,
            AnalysisJson = "{}",
            ReceivedAtUtc = clock.UtcNow,
            ResolvedAtUtc = clock.UtcNow,
        };

        context.AzureMonitorAlertEvents.Add(alertEvent);
        await context.SaveChangesAsync();

        var notificationService = new NotificationService(
            context,
            new TestTenantProvider(Guid.Empty),
            new TestCurrentUserProvider(currentUserId),
            new NotificationRealtimePublisher(),
            clock);

        var service = new AlertProcessingService(
            context,
            new TestAlertAnalysisWorkflow(),
            new PlatformAdminAlertAudienceResolver(context),
            notificationService,
            clock,
            NullLogger<AlertProcessingService>.Instance);

        await service.ProcessAsync(alertEvent.Id);

        var persistedAlert = await context.AzureMonitorAlertEvents.SingleAsync();
        persistedAlert.Status.Should().Be(AzureMonitorAlertStatuses.Processed, persistedAlert.LastError);
        persistedAlert.AnalysisSummary.Should().Be("The alert condition has recovered and current telemetry indicates the platform has stabilized.");

        var notification = await context.Notifications.SingleAsync();
        notification.TenantId.Should().Be(Guid.Empty);
        notification.UserId.Should().Be(platformAdminUserId);
        notification.ActionUrl.Should().Be($"/admin/alerts/{alertEvent.Id}");
        notification.Severity.Should().Be(NotificationSeverities.Success);
        notification.Type.Should().Be(AzureMonitorAlertTypes.PlatformPerformanceResolved);
    }

    private static AzureMonitorAlertWebhookRequest CreateWebhookRequest(string monitorCondition)
        => new(
            "azureMonitorCommonAlertSchema",
            new AzureMonitorAlertWebhookData(
                new AzureMonitorAlertEssentials(
                    AlertId: $"alert-{monitorCondition}",
                    AlertRule: "prod-api-5xx-spike",
                    Severity: "Sev2",
                    SignalType: "Log",
                    MonitorCondition: monitorCondition,
                    MonitoringService: "Application Insights",
                    AlertTargetIDs: ["/subscriptions/test/resourceGroups/rg/providers/Microsoft.App/containerApps/api"],
                    ConfigurationItems: null,
                    FiredDateTime: "2026-04-01T11:55:00Z",
                    ResolvedDateTime: monitorCondition == "Resolved" ? "2026-04-01T12:00:00Z" : null,
                    Description: "API 5xx spike",
                    InvestigationLink: "https://portal.azure.com/",
                    EssentialsVersion: "1.0",
                    AlertContextVersion: "1.0",
                    OriginAlertId: null),
                AlertContext: new { searchResults = 5 },
                CustomProperties: new Dictionary<string, string>
                {
                    ["alertCategory"] = "performance",
                    ["environmentName"] = "prod",
                }));

    private static PlatformDbContext CreateDbContext(Guid tenantId, Guid userId, IClock clock)
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"AzureMonitorAlertServices_{Guid.NewGuid()}")
            .Options;

        return new PlatformDbContext(
            options,
            new TestTenantProvider(tenantId),
            new TestCurrentUserProvider(userId),
            clock);
    }

    private sealed class TestAlertProcessingQueue : IAlertProcessingQueue
    {
        public List<Guid> EnqueuedAlertIds { get; } = [];

        public ValueTask EnqueueAsync(Guid alertId, CancellationToken cancellationToken = default)
        {
            EnqueuedAlertIds.Add(alertId);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestAlertAnalysisWorkflow : IAlertAnalysisWorkflow
    {
        public Task<AlertAnalysisResult> AnalyzeAsync(AzureMonitorAlertEvent alertEvent, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AlertAnalysisResult(
                AiRunId: null,
                Summary: "The alert condition has recovered and current telemetry indicates the platform has stabilized.",
                LikelyCause: "The underlying error rate fell back below the alert threshold.",
                Impact: "The platform is no longer actively degraded, but recent deployments should be reviewed.",
                AffectedComponent: "API container app",
                RecommendedActions: ["Confirm the service remains healthy for the next evaluation window."],
                Confidence: "High"));
        }
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
}
