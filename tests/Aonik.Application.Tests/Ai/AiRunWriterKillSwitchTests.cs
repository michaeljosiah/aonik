using Aonik.Ai.Entities;
using Aonik.Ai.Observability;
using Aonik.Ai.Persistence;
using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Verifies the kill-switch enforcement added to <see cref="AiRunWriter"/>:
/// when a tenant has <see cref="TenantAgentSettings.KillSwitchEngaged"/> set,
/// every <c>StartRunAsync</c> call short-circuits with
/// <see cref="KillSwitchEngagedException"/> before any DB write happens.
/// </summary>
public class AiRunWriterKillSwitchTests
{
    private static readonly Guid EngagedTenantId = Guid.Parse("ab000000-0000-0000-0000-000000000001");
    private static readonly Guid CleanTenantId = Guid.Parse("ab000000-0000-0000-0000-000000000002");
    private static readonly Guid CallingUserId = Guid.Parse("ab100000-0000-0000-0000-000000000099");

    private sealed class FixedTenantProvider : ITenantProvider
    {
        public Guid TenantId { get; init; }
        public Guid GetCurrentTenantId() => TenantId;
        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = TenantId;
            return true;
        }
    }

    private sealed class FixedUserProvider : ICurrentUserProvider
    {
        public Guid? UserId { get; init; }
        public Guid? GetCurrentUserId() => UserId;
        public bool TryGetCurrentUserId(out Guid userId)
        {
            userId = UserId ?? Guid.Empty;
            return UserId.HasValue;
        }
    }

    private static AiDbContext CreateDbContext(ITenantProvider tenantProvider)
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase($"AiRunWriter_KillSwitch_{Guid.NewGuid()}")
            .Options;
        // Tenant provider is required by AonikDbContextBase.EnforceTenantOnWrites
        // and drives the per-tenant query filter on TenantAgentSettings.
        return new AiDbContext(options, tenantProvider);
    }

    [Fact]
    public async Task StartRunAsync_Should_ThrowKillSwitchEngagedException_When_TenantHasSwitchEngaged()
    {
        var tenantProvider = new FixedTenantProvider { TenantId = EngagedTenantId };
        await using var dbContext = CreateDbContext(tenantProvider);

        var engagedAt = new DateTime(2026, 4, 27, 10, 0, 0, DateTimeKind.Utc);
        dbContext.TenantAgentSettings.Add(new TenantAgentSettings
        {
            Id = Guid.NewGuid(),
            TenantId = EngagedTenantId,
            KillSwitchEngaged = true,
            KillSwitchEngagedAt = engagedAt,
            KillSwitchEngagedByUserId = CallingUserId,
        });
        await dbContext.SaveChangesAsync();

        var writer = new AiRunWriter(
            dbContext,
            tenantProvider,
            new FixedUserProvider { UserId = CallingUserId },
            CreateFusionCache(),
            new AiRunMetrics());

        var act = async () => await writer.StartRunAsync(
            useCase: "test-usecase",
            inputRefsJson: "{}");

        var ex = await act.Should().ThrowAsync<KillSwitchEngagedException>();
        ex.Which.TenantId.Should().Be(EngagedTenantId);
        ex.Which.EngagedAt.Should().Be(engagedAt);
        ex.Which.EngagedByUserId.Should().Be(CallingUserId);

        // The run record must NOT have been written: blocked work doesn't
        // get a ghost AiRun row.
        (await dbContext.AiRuns.AcrossTenants().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task StartRunAsync_Should_RunNormally_When_NoSettingsRowExists()
    {
        var tenantProvider = new FixedTenantProvider { TenantId = CleanTenantId };
        await using var dbContext = CreateDbContext(tenantProvider);

        var writer = new AiRunWriter(
            dbContext,
            tenantProvider,
            new FixedUserProvider { UserId = CallingUserId },
            CreateFusionCache(),
            new AiRunMetrics());

        var runId = await writer.StartRunAsync("test-usecase", "{}");

        runId.Should().NotBe(Guid.Empty);
        var run = await dbContext.AiRuns.FirstAsync();
        run.TenantId.Should().Be(CleanTenantId);
        run.UseCase.Should().Be("test-usecase");
        run.Outcome.Should().Be("Started");
    }

    [Fact]
    public async Task StartRunAsync_Should_RunNormally_When_KillSwitchIsDisengaged()
    {
        var tenantProvider = new FixedTenantProvider { TenantId = CleanTenantId };
        await using var dbContext = CreateDbContext(tenantProvider);

        // Row exists but kill switch is OFF — same as missing row, runs proceed.
        dbContext.TenantAgentSettings.Add(new TenantAgentSettings
        {
            Id = Guid.NewGuid(),
            TenantId = CleanTenantId,
            KillSwitchEngaged = false,
        });
        await dbContext.SaveChangesAsync();

        var writer = new AiRunWriter(
            dbContext,
            tenantProvider,
            new FixedUserProvider { UserId = CallingUserId },
            CreateFusionCache(),
            new AiRunMetrics());

        var runId = await writer.StartRunAsync("test-usecase", "{}");
        runId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task StartRunAsync_Should_OnlyEnforceForCallingTenant()
    {
        // Sanity check that tenant scoping is correct: a switch engaged on
        // tenant A does not block runs on tenant B. The query filter only
        // sees the calling tenant's TenantAgentSettings row.
        var cleanProvider = new FixedTenantProvider { TenantId = CleanTenantId };
        await using var dbContext = CreateDbContext(cleanProvider);

        // Seed a settings row for ENGAGED tenant. The clean-tenant query in
        // StartRunAsync will not see it because of the per-tenant filter.
        dbContext.TenantAgentSettings.Add(new TenantAgentSettings
        {
            Id = Guid.NewGuid(),
            TenantId = EngagedTenantId,
            KillSwitchEngaged = true,
            KillSwitchEngagedAt = DateTime.UtcNow,
            KillSwitchEngagedByUserId = CallingUserId,
        });
        await dbContext.SaveChangesAsync();

        var writer = new AiRunWriter(
            dbContext,
            cleanProvider,
            new FixedUserProvider { UserId = CallingUserId },
            CreateFusionCache(),
            new AiRunMetrics());

        var runId = await writer.StartRunAsync("test-usecase", "{}");
        runId.Should().NotBe(Guid.Empty);
    }

    /// <summary>
    /// Each test gets its own in-memory FusionCache so cached kill-switch
    /// state from one scenario does not bleed into another. Mirrors the
    /// pattern used by <c>AgentConfigurationServiceTests</c>.
    /// </summary>
    private static IFusionCache CreateFusionCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFusionCache>();
    }
}
