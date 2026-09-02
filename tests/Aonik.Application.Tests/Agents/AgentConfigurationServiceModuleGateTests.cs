using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 097 §12.1: the by-name paths of <see cref="AgentConfigurationService"/> (read, upsert,
/// delete) refuse a code-based agent whose module is off for the current tenant, so configuration
/// of a disabled module's agent is neither readable nor writable even though the list already hid
/// it. Custom / DB-only agents (no descriptor) are untouched by the gate.
/// </summary>
public class AgentConfigurationServiceModuleGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task GetResolvedAsync_Should_ThrowModuleDisabled_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        await using var db = CreateDb();
        var service = CreateService(db, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var act = () => service.GetResolvedAsync("commerce-agent");

        // Assert
        (await act.Should().ThrowAsync<ModuleDisabledException>()).Which.ModuleId.Should().Be(ModuleIds.Commerce);
    }

    [Fact]
    public async Task UpsertOverrideAsync_Should_ThrowModuleDisabledAndWriteNothing_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        await using var db = CreateDb();
        var service = CreateService(db, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var act = () => service.UpsertOverrideAsync("commerce-agent", new UpsertAgentConfigurationRequest { InstructionsText = "x" });

        // Assert
        await act.Should().ThrowAsync<ModuleDisabledException>();
        (await db.Agents.AsNoTracking().CountAsync()).Should().Be(0, "nothing may be written for a disabled module's agent");
    }

    [Fact]
    public async Task DeleteOverrideAsync_Should_ThrowModuleDisabled_When_AgentModuleIsOffForTenant()
    {
        // Arrange
        await using var db = CreateDb();
        var service = CreateService(db, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var act = () => service.DeleteOverrideAsync("commerce-agent");

        // Assert
        await act.Should().ThrowAsync<ModuleDisabledException>();
    }

    [Fact]
    public async Task ByNamePaths_Should_Work_When_AgentModuleIsOnForTenant()
    {
        // Arrange
        await using var db = CreateDb();
        var service = CreateService(db, new FakeReader(disabled: ModuleIds.Workspaces));

        // Act
        var upserted = await service.UpsertOverrideAsync("commerce-agent", new UpsertAgentConfigurationRequest { InstructionsText = "Sell well." });
        var resolved = await service.GetResolvedAsync("commerce-agent");
        await service.DeleteOverrideAsync("commerce-agent");

        // Assert
        upserted.InstructionsText.Should().Be("Sell well.");
        resolved.Should().NotBeNull();
        resolved!.InstructionsText.Should().Be("Sell well.");
        (await db.Agents.AsNoTracking().CountAsync()).Should().Be(0, "the override was deleted");
    }

    [Fact]
    public async Task ByNamePaths_Should_IgnoreTheGate_When_AgentHasNoCodeDescriptor()
    {
        // Arrange — a custom / DB-only agent is never gated: there is no module to check.
        await using var db = CreateDb();
        var service = CreateService(db, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var resolved = await service.GetResolvedAsync("custom-agent");
        var upserted = await service.UpsertOverrideAsync("custom-agent", new UpsertAgentConfigurationRequest { Description = "Custom" });

        // Assert
        resolved.Should().BeNull("no row and no descriptor");
        upserted.Description.Should().Be("Custom");
    }

    private static AgentsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(TenantId));
    }

    private static AgentConfigurationService CreateService(AgentsDbContext db, IModuleEnablementReader reader)
    {
        var tenantProvider = new TestTenantProvider(TenantId);
        var filter = new DescriptorModuleFilter(tenantProvider, NullLogger<DescriptorModuleFilter>.Instance, reader);
        var services = new ServiceCollection();
        services.AddFusionCache();
        var cache = services.BuildServiceProvider().GetRequiredService<IFusionCache>();

        return new AgentConfigurationService(
            db,
            tenantProvider,
            Mock.Of<IAiModelResolver>(),
            [new FakeDescriptor("commerce-agent", ModuleIds.Commerce)],
            cache,
            NullLogger<AgentConfigurationService>.Instance,
            filter);
    }

    private sealed class TestTenantProvider(Guid tenantId) : ITenantProvider
    {
        public Guid GetCurrentTenantId() => tenantId;
        public bool TryGetCurrentTenantId(out Guid id) { id = tenantId; return true; }
    }

    private sealed class FakeDescriptor(string name, string? moduleId) : IDomainAgentDescriptor
    {
        public string Name => name;
        public string Description => $"Fake agent '{name}'.";
        public string? ModuleId => moduleId;

        public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
            => throw new NotSupportedException("Not built in this test.");

        public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) => [];
    }

    private sealed class FakeReader(params string[] disabled) : IModuleEnablementReader
    {
        public Task<ModuleEnablementSet> GetAsync(Guid tenantId, CancellationToken ct = default)
        {
            var enabled = ModuleCatalog.All.Select(m => m.Id).Except(disabled, StringComparer.Ordinal)
                .ToHashSet(StringComparer.Ordinal);
            return Task.FromResult(new ModuleEnablementSet(tenantId, enabled));
        }

        public Task<IReadOnlyList<Guid>> FilterEnabledTenantsAsync(
            IEnumerable<Guid> tenantIds, string moduleId, CancellationToken ct = default)
        {
            IReadOnlyList<Guid> result = disabled.Contains(moduleId, StringComparer.Ordinal) ? [] : tenantIds.Distinct().ToList();
            return Task.FromResult(result);
        }
    }
}
