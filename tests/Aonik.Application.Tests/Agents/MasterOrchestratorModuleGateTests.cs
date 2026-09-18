using System.Diagnostics.CodeAnalysis;

using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Modules;

using FluentAssertions;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Spec 097 §12.1 / acceptance 10 for the orchestrator: the set of domain agents it turns into
/// delegation tools is the registered descriptors minus those whose module is off for the current
/// tenant. Built through the real constructor so a DI-shaped regression (dropping the filter from
/// the pipeline) fails here.
/// </summary>
public class MasterOrchestratorModuleGateTests
{
    private static readonly Guid TenantId = Guid.NewGuid();

    [Fact]
    public async Task ResolveDelegableDescriptorsAsync_Should_OmitCommerceAgent_When_CommerceIsOffForTenant()
    {
        // Arrange
        var descriptors = new IDomainAgentDescriptor[]
        {
            new FakeDescriptor("finance-agent", ModuleIds.Finance),
            new FakeDescriptor("commerce-agent", ModuleIds.Commerce),
            new FakeDescriptor("platform-agent", ModuleIds.Platform),
        };
        var orchestrator = CreateOrchestrator(descriptors, new FakeReader(disabled: ModuleIds.Commerce));

        // Act
        var delegable = await orchestrator.ResolveDelegableDescriptorsAsync(CancellationToken.None);

        // Assert
        delegable.Select(d => d.Name).Should().BeEquivalentTo(["finance-agent", "platform-agent"]);
    }

    [Fact]
    public async Task ResolveDelegableDescriptorsAsync_Should_OmitFinanceAndCommerceAgents_When_FinanceIsOffForTenant()
    {
        // Arrange — commerce hard-depends on finance, so a reader that resolves finance off resolves
        // commerce off too (ModuleCatalog.ResolveEnabled); the fake mirrors that closure.
        var descriptors = new IDomainAgentDescriptor[]
        {
            new FakeDescriptor("finance-agent", ModuleIds.Finance),
            new FakeDescriptor("commerce-agent", ModuleIds.Commerce),
            new FakeDescriptor("platform-agent", ModuleIds.Platform),
        };
        var orchestrator = CreateOrchestrator(descriptors, new FakeReader(disabled: [ModuleIds.Finance, ModuleIds.Commerce]));

        // Act
        var delegable = await orchestrator.ResolveDelegableDescriptorsAsync(CancellationToken.None);

        // Assert
        delegable.Select(d => d.Name).Should().BeEquivalentTo(["platform-agent"]);
    }

    [Fact]
    public async Task ResolveDelegableDescriptorsAsync_Should_KeepEveryAgent_When_NoReaderIsRegistered()
    {
        // Arrange
        var descriptors = new IDomainAgentDescriptor[]
        {
            new FakeDescriptor("finance-agent", ModuleIds.Finance),
            new FakeDescriptor("commerce-agent", ModuleIds.Commerce),
        };
        var orchestrator = CreateOrchestrator(descriptors, reader: null);

        // Act
        var delegable = await orchestrator.ResolveDelegableDescriptorsAsync(CancellationToken.None);

        // Assert
        delegable.Should().HaveCount(2, "a host without the module graph delegates to every agent, as before");
    }

    private static MasterOrchestratorService CreateOrchestrator(IEnumerable<IDomainAgentDescriptor> descriptors, IModuleEnablementReader? reader)
    {
        var tenantProvider = new TestTenantProvider(TenantId);
        var filter = new DescriptorModuleFilter(tenantProvider, NullLogger<DescriptorModuleFilter>.Instance, reader);
        var currentUser = new Mock<ICurrentUserProvider>();
        currentUser.Setup(c => c.GetCurrentUserId()).Returns(Guid.NewGuid());

        return new MasterOrchestratorService(
            descriptors,
            filter,
            Mock.Of<IMcpToolProvider>(),
            Mock.Of<IAgentConfigurationService>(),
            Mock.Of<IAiModelResolver>(),
            Mock.Of<IChatClient>(),
            new ServiceCollection().BuildServiceProvider(),
            currentUser.Object,
            Mock.Of<IChatThreadService>(),
            Mock.Of<IChatThreadTitleGenerator>(),
            new NullSessionStore(),
            new ConfigurationBuilder().Build(),
            NullLogger<MasterOrchestratorService>.Instance);
    }

    /// <summary>The session store interface is internal, so Moq cannot proxy it; nothing here touches sessions.</summary>
    private sealed class NullSessionStore : IOrchestratorSessionStore
    {
        public bool TryGet(string sessionId, [NotNullWhen(true)] out AgentSession? session)
        {
            session = null;
            return false;
        }

        public AgentSession GetOrAdd(string sessionId, AgentSession session) => session;
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
