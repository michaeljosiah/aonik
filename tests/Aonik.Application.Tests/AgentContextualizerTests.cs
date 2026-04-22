using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.Agents.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ZiggyCreatures.Caching.Fusion;
using AgentUserBrief = Aonik.Agents.Contracts.Models.UserBrief;
using AgentUserBriefAmount = Aonik.Agents.Contracts.Models.UserBriefAmount;
using AgentUserBriefCash = Aonik.Agents.Contracts.Models.UserBriefCash;
using AgentUserBriefPeriod = Aonik.Agents.Contracts.Models.UserBriefPeriod;
using AgentUserBriefSignal = Aonik.Agents.Contracts.Models.UserBriefSignal;
using AgentUserBriefUser = Aonik.Agents.Contracts.Models.UserBriefUser;

namespace Aonik.Application.Tests;

public class AgentContextualizerTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReuseCachedUserBriefAcrossServiceInstances_WhenAgentRequiresIt()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var descriptor = new StubAgentDescriptor("personal-finance-agent", requiresUserBrief: true);
        var resolver = new StubDomainAgentResolver(descriptor);
        var projector = new StubUserBriefProjector();
        var cache = CreateFusionCache();

        var first = CreateSut(descriptor, resolver, projector, cache, tenantId, userId);
        var second = CreateSut(descriptor, resolver, projector, cache, tenantId, userId);

        // Act
        var firstResolution = await first.ResolveAsync(descriptor.Name, CancellationToken.None);
        var secondResolution = await second.ResolveAsync(descriptor.Name, CancellationToken.None);

        // Assert
        firstResolution.UserBriefPreamble.Should().NotBeNull();
        firstResolution.UserBriefCacheStatus.Should().Be("miss");
        firstResolution.UserBriefDurationMs.Should().NotBeNull();

        secondResolution.UserBriefPreamble.Should().NotBeNull();
        secondResolution.UserBriefCacheStatus.Should().Be("hit");
        secondResolution.UserBriefDurationMs.Should().NotBeNull();

        projector.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ResolveAsync_ShouldSkipUserBrief_WhenAgentDoesNotRequireIt()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var descriptor = new StubAgentDescriptor("finance-agent", requiresUserBrief: false);
        var resolver = new StubDomainAgentResolver(descriptor);
        var projector = new StubUserBriefProjector();

        var sut = CreateSut(descriptor, resolver, projector, CreateFusionCache(), tenantId, userId);

        // Act
        var resolution = await sut.ResolveAsync(descriptor.Name, CancellationToken.None);

        // Assert
        resolution.UserBriefPreamble.Should().BeNull();
        resolution.UserBriefCacheStatus.Should().Be("skipped");
        resolution.UserBriefDurationMs.Should().BeNull();
        projector.CallCount.Should().Be(0);
    }

    private static AgentContextualizer CreateSut(
        IDomainAgentDescriptor descriptor,
        IDomainAgentResolver resolver,
        IUserBriefProjector projector,
        IFusionCache cache,
        Guid tenantId,
        Guid userId)
    {
        return new AgentContextualizer(
            resolver,
            [descriptor],
            new StubMasterOrchestratorService(),
            projector,
            cache,
            NullLogger<AgentContextualizer>.Instance,
            new StubCurrentUserProvider(userId),
            new StubTenantProvider(tenantId));
    }

    private static IFusionCache CreateFusionCache()
    {
        var services = new ServiceCollection();
        services.AddFusionCache();
        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IFusionCache>();
    }

    private sealed class StubDomainAgentResolver : IDomainAgentResolver
    {
        private readonly AIAgent _agent;
        private readonly IDomainAgentDescriptor _descriptor;

        public StubDomainAgentResolver(IDomainAgentDescriptor descriptor)
        {
            _descriptor = descriptor;
            _agent = new ChatClientAgent(
                new FakeChatClient(),
                name: descriptor.Name,
                instructions: "Test instructions",
                tools: Array.Empty<AITool>());
        }

        public Task<(AIAgent Agent, IDomainAgentDescriptor Descriptor)> ResolveAsync(
            string agentId,
            CancellationToken cancellationToken = default)
            => Task.FromResult((_agent, _descriptor));
    }

    private sealed class StubAgentDescriptor : IDomainAgentDescriptor
    {
        public StubAgentDescriptor(string name, bool requiresUserBrief)
        {
            Name = name;
            RequiresUserBrief = requiresUserBrief;
        }

        public string Name { get; }

        public string Description => "Test agent";

        public bool RequiresUserBrief { get; }

        public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
            => new ChatClientAgent(
                chatClient,
                name: Name,
                instructions: "Test instructions",
                tools: Array.Empty<AITool>());

        public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) => [];
    }

    private sealed class StubUserBriefProjector : IUserBriefProjector
    {
        public int CallCount { get; private set; }

        public Task<AgentUserBrief> ProjectAsync(
            Guid tenantId,
            Guid userId,
            UserBriefOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;

            return Task.FromResult(new AgentUserBrief(
                AsOf: DateTimeOffset.UtcNow,
                User: new AgentUserBriefUser("Ada", "GB"),
                Goals: ["Stay on budget"],
                Cash: new AgentUserBriefCash(1200m, "GBP"),
                Period: new AgentUserBriefPeriod(3200m, 2400m, "GBP"),
                TopCategories: [new AgentUserBriefAmount("Food", 450m)],
                TopMerchants: [new AgentUserBriefAmount("Tesco", 120m)],
                Signals: [new AgentUserBriefSignal("Spending is stable", "low")],
                Risks: [],
                CashflowRisk: CashflowRisk.Low,
                MissingData: [],
                AiCanDo: ["Summarise spending"],
                AiNeedsApproval: ["Create a payment"]));
        }
    }

    private sealed class StubCurrentUserProvider : ICurrentUserProvider
    {
        private readonly Guid _userId;

        public StubCurrentUserProvider(Guid userId)
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

    private sealed class StubTenantProvider : ITenantProvider
    {
        private readonly Guid _tenantId;

        public StubTenantProvider(Guid tenantId)
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

    private sealed class StubMasterOrchestratorService : IMasterOrchestratorService
    {
        private readonly AIAgent _agent = new ChatClientAgent(
            new FakeChatClient(),
            name: "orchestrator",
            instructions: "Test instructions",
            tools: Array.Empty<AITool>());

        public Task<AgentChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<AIAgent> GetAgentAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_agent);
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
