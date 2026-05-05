using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Entities;
using Aonik.Agents.Framework;
using Aonik.Agents.Persistence;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Persistence;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Application.Tests;

public class AgentConfigurationServiceTests
{
    private class TestTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public TestTenantProvider(Guid tenantId) => _tenantId = tenantId;

        public Guid GetCurrentTenantId() => _tenantId;

        public bool TryGetCurrentTenantId(out Guid tenantId)
        {
            tenantId = _tenantId;
            return true;
        }
    }

    /// <summary>
    /// Stub model resolver that returns null for all lookups (no AI models configured in test).
    /// </summary>
    private sealed class StubModelResolver : IAiModelResolver
    {
        public Task<string?> ResolveModelNameAsync(string useCase, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);

        public Task<string?> ResolveModelNameByIdAsync(Guid modelId, CancellationToken cancellationToken = default)
            => Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Minimal descriptor for testing. Exposes known tool names and instructions.
    /// </summary>
    private sealed class StubAgentDescriptor : IDomainAgentDescriptor
    {
        public string Name { get; }
        public string Description { get; }
        public string? Instructions { get; }
        private readonly string[] _toolNames;

        public StubAgentDescriptor(string name, string description, string? instructions, string[] toolNames)
        {
            Name = name;
            Description = description;
            Instructions = instructions;
            _toolNames = toolNames;
        }

        public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider) =>
            throw new NotImplementedException("Not needed for config service tests");

        public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) =>
            _toolNames;
    }

    private static AgentsDbContext CreateDbContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AgentsDbContext>()
            .UseInMemoryDatabase(databaseName: $"AgentConfigTest_{Guid.NewGuid()}")
            .Options;
        return new AgentsDbContext(options, new TestTenantProvider(tenantId));
    }

    private static AgentConfigurationService CreateService(
        AgentsDbContext context,
        ITenantProvider tenantProvider,
        IEnumerable<IDomainAgentDescriptor>? descriptors = null,
        IAiModelResolver? modelResolver = null)
    {
        return new AgentConfigurationService(
            context,
            tenantProvider,
            modelResolver ?? new StubModelResolver(),
            descriptors ?? Array.Empty<IDomainAgentDescriptor>(),
            CreateFusionCache(),
            NullLogger<AgentConfigurationService>.Instance);
    }

    private static IFusionCache CreateFusionCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFusionCache>();
    }

    [Fact]
    public async Task SeedGlobalDefaultsAsync_ShouldCreateGlobalRows_ForEachDescriptor()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var descriptors = new IDomainAgentDescriptor[]
        {
            new StubAgentDescriptor("test-agent", "Test agent", "You are a test agent.", new[] { "test_read", "test_create_item" }),
            new StubAgentDescriptor("read-only-agent", "Read-only agent", "You are read-only.", new[] { "ro_list", "ro_get" })
        };
        var service = CreateService(context, new TestTenantProvider(tenantId), descriptors);
        var sp = new ServiceCollection().BuildServiceProvider();

        // Act
        await service.SeedGlobalDefaultsAsync(sp);

        // Assert
        var agents = await context.Agents.IncludeSoftDeleted().ToListAsync();
        agents.Should().HaveCount(2);

        var testAgent = agents.First(a => a.Name == "test-agent");
        testAgent.TenantId.Should().BeNull();
        testAgent.Description.Should().Be("Test agent");
        testAgent.InstructionsText.Should().Be("You are a test agent.");
        testAgent.RiskTier.Should().Be("medium"); // has test_create_item (mutation verb)
        testAgent.IsActive.Should().BeTrue();
        testAgent.ToolsetIdsJson.Should().Contain("test_read");
        testAgent.ToolsetIdsJson.Should().Contain("test_create_item");

        var readOnlyAgent = agents.First(a => a.Name == "read-only-agent");
        readOnlyAgent.RiskTier.Should().Be("low"); // no mutation verbs
    }

    [Fact]
    public async Task SeedGlobalDefaultsAsync_ShouldBeIdempotent()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var descriptors = new IDomainAgentDescriptor[]
        {
            new StubAgentDescriptor("test-agent", "Test agent", "Instructions.", new[] { "tool_a" })
        };
        var service = CreateService(context, new TestTenantProvider(tenantId), descriptors);
        var sp = new ServiceCollection().BuildServiceProvider();

        // Act — seed twice
        await service.SeedGlobalDefaultsAsync(sp);
        await service.SeedGlobalDefaultsAsync(sp);

        // Assert — only one row
        var agents = await context.Agents.IncludeSoftDeleted().ToListAsync();
        agents.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetResolvedAsync_ShouldReturnNull_WhenNoConfigExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Act
        var result = await service.GetResolvedAsync("nonexistent-agent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetResolvedAsync_ShouldReturnGlobalDefault_WhenNoTenantOverride()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Seed a global default directly
        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "test-agent",
            Domain = "test",
            Description = "Global description",
            InstructionsText = "Global instructions",
            ToolsetIdsJson = "[\"tool_a\"]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetResolvedAsync("test-agent");

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Global description");
        result.InstructionsText.Should().Be("Global instructions");
        result.IsOverride.Should().BeFalse();
    }

    [Fact]
    public async Task GetResolvedAsync_ShouldReturnTenantOverride_WhenExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Seed global default
        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "test-agent",
            Domain = "test",
            Description = "Global description",
            InstructionsText = "Global instructions",
            ToolsetIdsJson = "[\"tool_a\"]",
            RiskTier = "low",
            IsActive = true
        });

        // Add tenant override
        context.Agents.Add(new Agent
        {
            TenantId = tenantId,
            Name = "test-agent",
            Domain = "test",
            Description = "Tenant description",
            InstructionsText = "Tenant instructions",
            ToolsetIdsJson = "[\"tool_a\",\"tool_b\"]",
            RiskTier = "medium",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        var result = await service.GetResolvedAsync("test-agent");

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Tenant description");
        result.InstructionsText.Should().Be("Tenant instructions");
        result.IsOverride.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertOverrideAsync_ShouldCreateNewTenantOverride()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var descriptors = new IDomainAgentDescriptor[]
        {
            new StubAgentDescriptor("test-agent", "Code description", "Code instructions", new[] { "tool_a" })
        };
        var service = CreateService(context, new TestTenantProvider(tenantId), descriptors);

        // Seed global default
        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "test-agent",
            Domain = "test",
            Description = "Global description",
            InstructionsText = "Global instructions",
            ToolsetIdsJson = "[\"tool_a\"]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var request = new UpsertAgentConfigurationRequest
        {
            Description = "My custom description",
            IsActive = false
        };

        // Act
        var result = await service.UpsertOverrideAsync("test-agent", request);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(tenantId);
        result.Description.Should().Be("My custom description");
        result.IsActive.Should().BeFalse();
        // Fields not provided in request should come from global default
        result.InstructionsText.Should().Be("Global instructions");
        result.RiskTier.Should().Be("low");
        result.IsOverride.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertOverrideAsync_ShouldUpdateExistingTenantOverride()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Create initial tenant override
        context.Agents.Add(new Agent
        {
            TenantId = tenantId,
            Name = "test-agent",
            Domain = "test",
            Description = "Original description",
            InstructionsText = "Original instructions",
            ToolsetIdsJson = "[\"tool_a\"]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        var request = new UpsertAgentConfigurationRequest
        {
            Description = "Updated description"
        };

        // Act
        var result = await service.UpsertOverrideAsync("test-agent", request);

        // Assert
        result.Description.Should().Be("Updated description");
        result.InstructionsText.Should().Be("Original instructions"); // unchanged
        result.IsActive.Should().BeTrue(); // unchanged
    }

    [Fact]
    public async Task DeleteOverrideAsync_ShouldRemoveTenantOverride()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Create tenant override
        context.Agents.Add(new Agent
        {
            TenantId = tenantId,
            Name = "test-agent",
            Domain = "test",
            Description = "Tenant description",
            InstructionsText = "Tenant instructions",
            ToolsetIdsJson = "[]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        await service.DeleteOverrideAsync("test-agent");

        // Assert — tenant override should be soft-deleted (AonikDbContextBase converts Remove to soft-delete)
        var softDeleted = await context.Agents.IncludeSoftDeleted()
            .Where(a => a.Name == "test-agent" && a.TenantId == tenantId)
            .SingleAsync();
        softDeleted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOverrideAsync_ShouldNotThrow_WhenNoOverrideExists()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Act — should not throw
        await service.DeleteOverrideAsync("nonexistent-agent");

        // Assert — no exception means success
    }

    [Fact]
    public async Task ListAsync_ShouldReturnAllVisibleConfigurations()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var service = CreateService(context, new TestTenantProvider(tenantId));

        // Seed global + tenant configs
        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "agent-a",
            Domain = "test",
            Description = "Global A",
            InstructionsText = "",
            ToolsetIdsJson = "[]",
            RiskTier = "low",
            IsActive = true
        });
        context.Agents.Add(new Agent
        {
            TenantId = tenantId,
            Name = "agent-a",
            Domain = "test",
            Description = "Tenant A",
            InstructionsText = "",
            ToolsetIdsJson = "[]",
            RiskTier = "low",
            IsActive = true
        });
        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "agent-b",
            Domain = "test",
            Description = "Global B",
            InstructionsText = "",
            ToolsetIdsJson = "[]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Act
        var results = await service.ListAsync();

        // Assert — should see 2 (agent-a resolved to tenant override, agent-b from global)
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.Name == "agent-a" && r.IsOverride);
        results.Should().Contain(r => r.Name == "agent-b" && !r.IsOverride);
    }

    [Fact]
    public async Task SeedGlobalDefaultsAsync_ShouldDetectMutatingToolsByNameConvention()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var descriptors = new IDomainAgentDescriptor[]
        {
            new StubAgentDescriptor("mutating-agent", "Has mutations", "Inst.",
                new[] { "pf_list_items", "pf_create_item", "pf_archive_item" }),
            new StubAgentDescriptor("safe-agent", "Read only", "Inst.",
                new[] { "pf_list_items", "pf_get_item", "pf_get_summary" })
        };
        var service = CreateService(context, new TestTenantProvider(tenantId), descriptors);
        var sp = new ServiceCollection().BuildServiceProvider();

        // Act
        await service.SeedGlobalDefaultsAsync(sp);

        // Assert
        var agents = await context.Agents.IncludeSoftDeleted().ToListAsync();
        agents.First(a => a.Name == "mutating-agent").RiskTier.Should().Be("medium");
        agents.First(a => a.Name == "safe-agent").RiskTier.Should().Be("low");
    }

    [Fact]
    public async Task GetResolvedAsync_ShouldCacheResult_SoSecondCallDoesNotHitStore()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var cache = CreateFusionCache();
        var service = new AgentConfigurationService(
            context,
            new TestTenantProvider(tenantId),
            new StubModelResolver(),
            Array.Empty<IDomainAgentDescriptor>(),
            cache,
            NullLogger<AgentConfigurationService>.Instance);

        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "cache-agent",
            Domain = "test",
            Description = "Original",
            InstructionsText = "Original",
            ToolsetIdsJson = "[]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // First call — populates cache
        var first = await service.GetResolvedAsync("cache-agent");
        first.Should().NotBeNull();
        first!.Description.Should().Be("Original");

        // Mutate the underlying row *without* going through the service,
        // so the cache should not be invalidated.
        var row = await context.Agents.IncludeSoftDeleted()
            .SingleAsync(a => a.Name == "cache-agent" && a.TenantId == null);
        row.Description = "Changed behind the service's back";
        await context.SaveChangesAsync();

        // Act — second call should return the cached (original) value.
        var second = await service.GetResolvedAsync("cache-agent");

        // Assert
        second.Should().NotBeNull();
        second!.Description.Should().Be("Original");
    }

    [Fact]
    public async Task UpsertOverrideAsync_ShouldInvalidateResolvedCache()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        using var context = CreateDbContext(tenantId);
        var cache = CreateFusionCache();
        var service = new AgentConfigurationService(
            context,
            new TestTenantProvider(tenantId),
            new StubModelResolver(),
            Array.Empty<IDomainAgentDescriptor>(),
            cache,
            NullLogger<AgentConfigurationService>.Instance);

        context.Agents.Add(new Agent
        {
            TenantId = null,
            Name = "invalidation-agent",
            Domain = "test",
            Description = "Global",
            InstructionsText = "Global",
            ToolsetIdsJson = "[]",
            RiskTier = "low",
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Prime the cache
        var primed = await service.GetResolvedAsync("invalidation-agent");
        primed!.Description.Should().Be("Global");

        // Act — upsert a tenant override, which must invalidate the cache
        await service.UpsertOverrideAsync("invalidation-agent", new UpsertAgentConfigurationRequest
        {
            Description = "Tenant override",
            InstructionsText = "Tenant instructions"
        });

        // Assert — the next read must see the new value
        var refreshed = await service.GetResolvedAsync("invalidation-agent");
        refreshed.Should().NotBeNull();
        refreshed!.Description.Should().Be("Tenant override");
        refreshed.IsOverride.Should().BeTrue();
    }
}
