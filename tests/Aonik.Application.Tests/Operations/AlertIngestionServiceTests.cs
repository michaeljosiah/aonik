using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.Platform.Services.Operations;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Aonik.Application.Tests.Operations;

/// <summary>
/// Unit tests for <see cref="AlertIngestionService"/> — the inbound
/// webhook handler for Azure Monitor common-alert-schema posts. Covers
/// payload validation, idempotency on duplicate ExternalAlertId, the
/// receive-then-enqueue flow, and the rule-name → category inference
/// that drives <c>NormalizedType</c>.
/// xUnit + Moq + FluentAssertions per the project's standard testing stack.
/// </summary>
public class AlertIngestionServiceTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 5, 14, 30, 0, DateTimeKind.Utc);

    private readonly Mock<IAlertProcessingQueue> _queue;
    private readonly Mock<IClock> _clock;
    private readonly Mock<ILogger<AlertIngestionService>> _logger;

    public AlertIngestionServiceTests()
    {
        _queue = new Mock<IAlertProcessingQueue>();
        _queue
            .Setup(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        _clock = new Mock<IClock>();
        _clock.SetupGet(c => c.UtcNow).Returns(FixedNow);

        _logger = new Mock<ILogger<AlertIngestionService>>();
    }

    // ── Validation ─────────────────────────────────────────────────────

    [Fact]
    public async Task IngestAsync_Should_Throw_When_RequestIsNull()
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);

        var act = async () => await service.IngestAzureMonitorAlertAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task IngestAsync_Should_Reject_NonCommonAlertSchema()
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);

        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureLegacySchema",
            Data: BuildEssentials("alert-1"));

        var act = async () => await service.IngestAzureMonitorAlertAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Only azureMonitorCommonAlertSchema payloads are supported.*");
    }

    [Fact]
    public async Task IngestAsync_Should_Reject_When_EssentialsMissing()
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);

        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureMonitorCommonAlertSchema",
            Data: new AzureMonitorAlertWebhookData(
                Essentials: null,
                AlertContext: null,
                CustomProperties: null));

        var act = async () => await service.IngestAzureMonitorAlertAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*missing essentials data.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task IngestAsync_Should_Reject_When_AlertIdBlank(string? alertId)
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);

        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureMonitorCommonAlertSchema",
            Data: BuildEssentials(alertId));

        var act = async () => await service.IngestAzureMonitorAlertAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ── Happy path ─────────────────────────────────────────────────────

    [Fact]
    public async Task IngestAsync_Should_PersistEvent_And_EnqueueForProcessing()
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);
        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureMonitorCommonAlertSchema",
            Data: BuildEssentials(
                alertId: "alert-001",
                alertRule: "API: Errors elevated",
                severity: "Sev2"));

        var response = await service.IngestAzureMonitorAlertAsync(request);

        // Response carries the new event's id + initial status.
        response.AlertId.Should().NotBeEmpty();
        response.Status.Should().Be(AzureMonitorAlertStatuses.Received);

        // Row is persisted with global tenant scope (TenantId == Guid.Empty)
        // and a Received status, ready for the background worker.
        var persisted = await dbContext.Set<AzureMonitorAlertEvent>()
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == response.AlertId);
        persisted.ExternalAlertId.Should().Be("alert-001");
        persisted.AlertRuleName.Should().Be("API: Errors elevated");
        persisted.Severity.Should().Be("Sev2");
        persisted.Status.Should().Be(AzureMonitorAlertStatuses.Received);
        persisted.ReceivedAtUtc.Should().Be(FixedNow);
        persisted.TenantId.Should().Be(Guid.Empty,
            because: "platform-level alerts are not tenant-scoped");

        // The event is forwarded to the processing queue exactly once.
        _queue.Verify(q => q.EnqueueAsync(response.AlertId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestAsync_Should_BeIdempotent_On_DuplicateExternalAlertId()
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);
        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureMonitorCommonAlertSchema",
            Data: BuildEssentials(alertId: "alert-dupe"));

        var first = await service.IngestAzureMonitorAlertAsync(request);
        var second = await service.IngestAzureMonitorAlertAsync(request);

        // Same id returned both times — no new row, no second enqueue.
        second.AlertId.Should().Be(first.AlertId);

        var rowCount = await dbContext.Set<AzureMonitorAlertEvent>()
            .IgnoreQueryFilters()
            .CountAsync(e => e.ExternalAlertId == "alert-dupe");
        rowCount.Should().Be(1);

        _queue.Verify(q => q.EnqueueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Type inference (NormalizedType drives downstream routing) ──────

    [Theory]
    [InlineData("Security: Key Vault leak", AzureMonitorAlertTypes.PlatformSecurityAlert)]
    [InlineData("Worker job lag", AzureMonitorAlertTypes.PlatformOperationsAlert)]
    [InlineData("Availability dropped", AzureMonitorAlertTypes.PlatformAvailabilityAlert)]
    [InlineData("Latency P95 elevated", AzureMonitorAlertTypes.PlatformPerformanceAlert)]
    public async Task IngestAsync_Should_InferCategory_From_AlertRuleName(string alertRule, string expectedNormalizedType)
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);
        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureMonitorCommonAlertSchema",
            Data: BuildEssentials(alertId: $"alert-{Guid.NewGuid():N}", alertRule: alertRule));

        var response = await service.IngestAzureMonitorAlertAsync(request);

        var persisted = await dbContext.Set<AzureMonitorAlertEvent>()
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == response.AlertId);
        persisted.NormalizedType.Should().Be(expectedNormalizedType);
    }

    [Fact]
    public async Task IngestAsync_Should_FlipNormalizedType_To_Resolved_When_MonitorConditionIsResolved()
    {
        await using var dbContext = CreateDbContext();
        var service = NewService(dbContext);
        var request = new AzureMonitorAlertWebhookRequest(
            SchemaId: "azureMonitorCommonAlertSchema",
            Data: BuildEssentials(
                alertId: "alert-resolved",
                alertRule: "API: Errors elevated",
                monitorCondition: AzureMonitorAlertConditions.Resolved));

        var response = await service.IngestAzureMonitorAlertAsync(request);

        var persisted = await dbContext.Set<AzureMonitorAlertEvent>()
            .IgnoreQueryFilters()
            .FirstAsync(e => e.Id == response.AlertId);
        persisted.NormalizedType.Should().Be(AzureMonitorAlertTypes.PlatformPerformanceResolved);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private AlertIngestionService NewService(PlatformDbContext dbContext)
        => new(dbContext, _queue.Object, _clock.Object, _logger.Object);

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase($"AlertIngestion_{Guid.NewGuid()}")
            .Options;

        // The base DbContext requires a tenant context for ITenantScoped writes
        // — alerts are global (TenantId == Guid.Empty), and the production
        // background pipeline likewise sets TenantContext.TenantId to
        // Guid.Empty before invoking ingestion. Providing the empty tenant
        // here makes the EnforceTenantOnWrites check pass.
        var tenantProvider = new Mock<ITenantProvider>();
        tenantProvider.Setup(t => t.GetCurrentTenantId()).Returns(Guid.Empty);
        tenantProvider
            .Setup(t => t.TryGetCurrentTenantId(out It.Ref<Guid>.IsAny))
            .Callback(new TryGetCurrentTenantIdDelegate((out Guid id) => id = Guid.Empty))
            .Returns(true);

        return new PlatformDbContext(options, tenantProvider.Object);
    }

    private delegate void TryGetCurrentTenantIdDelegate(out Guid tenantId);

    private static AzureMonitorAlertWebhookData BuildEssentials(
        string? alertId,
        string? alertRule = "API: Errors elevated",
        string? severity = "Sev2",
        string? monitorCondition = "Fired")
    {
        return new AzureMonitorAlertWebhookData(
            Essentials: new AzureMonitorAlertEssentials(
                AlertId: alertId,
                AlertRule: alertRule,
                Severity: severity,
                SignalType: "Metric",
                MonitorCondition: monitorCondition,
                MonitoringService: "AzureMonitor",
                AlertTargetIDs: ["/subscriptions/sub-1/resourceGroups/rg-1/providers/Microsoft.App/containerApps/api"],
                ConfigurationItems: null,
                FiredDateTime: "2026-05-05T14:00:00Z",
                ResolvedDateTime: null,
                Description: "Errors elevated",
                InvestigationLink: null,
                EssentialsVersion: "1.0",
                AlertContextVersion: "1.0",
                OriginAlertId: null),
            AlertContext: null,
            CustomProperties: null);
    }
}
