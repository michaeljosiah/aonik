using System.Net;
using System.Net.Http.Json;
using Aonik.Platform.Contracts.Api.Jobs;
using Aonik.Platform.Entities.Operations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Api.Tests;

public class ScheduledJobAdminEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ScheduledJobAdminEndpointsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListScheduledJobs_ShouldReturnProjectionRows()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedProjectionAsync(tenantId, new ScheduledJobProjection
        {
            TenantId = Guid.Empty,
            JobName = "StaleSessionDetectorJob",
            GroupName = ScheduledJobGroups.ScheduledJobs,
            DisplayName = "Stale Session Detector",
            Description = "Detects stale sessions.",
            CronExpression = "0 0/5 * * * ?",
            TimeZoneId = "UTC",
            State = ScheduledJobStates.Active,
            NextFireTimeUtc = DateTime.UtcNow.AddMinutes(5),
            PreviousFireTimeUtc = DateTime.UtcNow.AddMinutes(-5),
            LastSyncedAtUtc = DateTime.UtcNow,
        });

        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithTenant(tenantId)
                .WithRoles("TenantAdmin"));

        // Act
        var response = await client.GetAsync("/admin/jobs/scheduled");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ScheduledJobListResponse>();
        payload.Should().NotBeNull();
        payload!.Jobs.Should().ContainSingle();
        payload.Jobs[0].JobName.Should().Be("StaleSessionDetectorJob");
        payload.Jobs[0].DisplayName.Should().Be("Stale Session Detector");
        payload.Jobs[0].Status.Should().Be(ScheduledJobStates.Active);
    }

    [Fact]
    public async Task TriggerScheduledJob_ShouldQueuePendingCommand()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        await SeedProjectionAsync(tenantId, new ScheduledJobProjection
        {
            TenantId = Guid.Empty,
            JobName = "CustomerInsightSnapshotJob",
            GroupName = ScheduledJobGroups.ScheduledJobs,
            DisplayName = "Customer Insight Snapshot",
            Description = "Generates deterministic customer insight snapshots.",
            CronExpression = "0 0/15 * * * ?",
            TimeZoneId = "UTC",
            State = ScheduledJobStates.Active,
            LastSyncedAtUtc = DateTime.UtcNow,
        });

        var client = await _factory.CreateAuthenticatedClientAsync(
            TestAuthOptions.Create()
                .WithTenant(tenantId)
                .WithRoles("TenantAdmin"));

        // Act
        var response = await client.PostAsync("/admin/jobs/scheduled/CustomerInsightSnapshotJob/trigger", content: null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var payload = await response.Content.ReadFromJsonAsync<ScheduledJobActionResponse>();
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.CommandId.Should().NotBeNull();
        payload.CommandStatus.Should().Be(ScheduledJobCommandStatuses.Pending);

        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var command = await dbContext.ScheduledJobAdminCommands.SingleAsync();
        command.JobName.Should().Be("CustomerInsightSnapshotJob");
        command.CommandType.Should().Be(ScheduledJobCommandTypes.Trigger);
        command.Status.Should().Be(ScheduledJobCommandStatuses.Pending);
    }

    private async Task SeedProjectionAsync(Guid tenantId, ScheduledJobProjection projection)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.TenantId = tenantId;
        tenantContext.ResolutionSource = "test";

        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        dbContext.ScheduledJobAdminCommands.RemoveRange(dbContext.ScheduledJobAdminCommands);
        dbContext.ScheduledJobProjections.RemoveRange(dbContext.ScheduledJobProjections);
        dbContext.ScheduledJobProjections.Add(projection);
        await dbContext.SaveChangesAsync();
    }
}
