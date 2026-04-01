using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Aonik.Platform.Contracts.Api.Operations;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class PlatformAlertEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PlatformAlertEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ReceiveAzureMonitorAlert_ShouldAcceptValidSharedSecretViaQueryString()
    {
        await ClearAlertsAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/integrations/azure/alerts?code=test-alert-secret",
            CreateWebhookRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var persisted = await dbContext.AzureMonitorAlertEvents.IgnoreQueryFilters().SingleAsync(x => x.ExternalAlertId == "alert-fired-1");
        persisted.ExternalAlertId.Should().Be("alert-fired-1");
    }

    [Fact]
    public async Task ReceiveAzureMonitorAlert_ShouldRejectInvalidSharedSecret()
    {
        await ClearAlertsAsync();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/integrations/azure/alerts?code=bad-secret",
            CreateWebhookRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAlerts_ShouldReturnGlobalAlertsForPlatformAdmin()
    {
        var tenantId = Guid.NewGuid();
        await SeedAlertAsync(new AzureMonitorAlertEvent
        {
            TenantId = Guid.Empty,
            Provider = AzureMonitorAlertProviders.AzureMonitor,
            ExternalAlertId = "alert-list-1",
            AlertRuleName = "prod-api-5xx-spike",
            AlertRuleId = "alert-list-1",
            MonitorCondition = AzureMonitorAlertConditions.Fired,
            Severity = "Sev2",
            SignalType = "Log",
            MonitoringService = "Application Insights",
            NormalizedType = AzureMonitorAlertTypes.PlatformPerformanceAlert,
            CorrelationKey = "corr-list-1",
            Status = AzureMonitorAlertStatuses.Processed,
            ResourceIdsJson = "[]",
            EssentialsJson = "{}",
            AlertContextJson = "{}",
            CustomPropertiesJson = "{}",
            AnalysisSummary = "API failure spike detected.",
            AnalysisJson = "{}",
            ReceivedAtUtc = DateTime.UtcNow,
        });

        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.GetAsync("/admin/alerts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AlertListResponse>();
        payload.Should().NotBeNull();
        payload!.Alerts.Should().ContainSingle();
        payload.Alerts[0].AlertRuleName.Should().Be("prod-api-5xx-spike");
    }

    [Fact]
    public async Task GetAlertDetail_ShouldReturnGlobalAlertForPlatformAdmin()
    {
        var tenantId = Guid.NewGuid();
        var alert = new AzureMonitorAlertEvent
        {
            TenantId = Guid.Empty,
            Provider = AzureMonitorAlertProviders.AzureMonitor,
            ExternalAlertId = "alert-detail-1",
            AlertRuleName = "prod-worker-exceptions",
            AlertRuleId = "alert-detail-1",
            MonitorCondition = AzureMonitorAlertConditions.Resolved,
            Severity = "Sev3",
            SignalType = "Log",
            MonitoringService = "Application Insights",
            NormalizedType = AzureMonitorAlertTypes.PlatformOperationsResolved,
            CorrelationKey = "corr-detail-1",
            Status = AzureMonitorAlertStatuses.Processed,
            ResourceIdsJson = "[]",
            EssentialsJson = "{}",
            AlertContextJson = "{}",
            CustomPropertiesJson = "{}",
            AnalysisSummary = "Worker health recovered.",
            AnalysisJson = "{}",
            ReceivedAtUtc = DateTime.UtcNow,
        };

        await SeedAlertAsync(alert);
        var client = await CreatePlatformAdminClientAsync(tenantId);

        var response = await client.GetAsync($"/admin/alerts/{alert.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<AlertDetailResponse>();
        payload.Should().NotBeNull();
        payload!.Id.Should().Be(alert.Id);
        payload.AlertRuleName.Should().Be("prod-worker-exceptions");
    }

    private async Task<HttpClient> CreatePlatformAdminClientAsync(Guid tenantId)
    {
        var options = TestAuthOptions.Create()
            .WithTenant(tenantId)
            .WithRoles("PlatformAdmin")
            .WithClaims(new Claim("roles", "Aonik.PlatformAdmin"));

        return await _factory.CreateAuthenticatedClientAsync(options);
    }

    private async Task ClearAlertsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.AzureMonitorAlertEvents.RemoveRange(dbContext.AzureMonitorAlertEvents.IgnoreQueryFilters());
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedAlertAsync(AzureMonitorAlertEvent alertEvent)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = Guid.Empty;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.AzureMonitorAlertEvents.RemoveRange(dbContext.AzureMonitorAlertEvents.IgnoreQueryFilters());
        dbContext.AzureMonitorAlertEvents.Add(alertEvent);
        await dbContext.SaveChangesAsync();
    }

    private static AzureMonitorAlertWebhookRequest CreateWebhookRequest()
        => new(
            "azureMonitorCommonAlertSchema",
            new AzureMonitorAlertWebhookData(
                new AzureMonitorAlertEssentials(
                    AlertId: "alert-fired-1",
                    AlertRule: "prod-api-5xx-spike",
                    Severity: "Sev2",
                    SignalType: "Log",
                    MonitorCondition: "Fired",
                    MonitoringService: "Application Insights",
                    AlertTargetIDs: ["/subscriptions/test/resourceGroups/rg/providers/Microsoft.App/containerApps/api"],
                    ConfigurationItems: null,
                    FiredDateTime: "2026-04-01T11:55:00Z",
                    ResolvedDateTime: null,
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
}
