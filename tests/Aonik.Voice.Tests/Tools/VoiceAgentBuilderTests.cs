using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.Voice.Tools;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Voice.Tests.Tools;

public class VoiceAgentBuilderTests
{
    [Fact]
    public void BuildReadOnlyVariant_Should_Produce_Result_That_Excludes_Mutating_Tools()
    {
        // Arrange — fake descriptor with mixed pf_* tool names. The fake's
        // Build(allowedToolNames) honours the filter exactly the way
        // PersonalFinanceAgentRegistration does (matches the contract on
        // IDomainAgentDescriptor:70).
        var descriptor = new FakeDomainAgentDescriptor(
            name: "personal-finance-agent",
            toolNames: new[]
            {
                "pf_get_accounts",       // ✓ allowed
                "pf_list_invoices",      // ✓ allowed
                "pf_search_payees",      // ✓ allowed
                "pf_create_invoice",     // ✗ removed
                "pf_update_account",     // ✗ removed
                "pf_apply_proposal",     // ✗ removed
                "user_memory_recall",    // ✗ removed (unknown → fail safe)
            });

        var services = BuildServiceProvider();
        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector());

        // Act
        var result = builder.BuildReadOnlyVariant(descriptor, services);

        // Assert
        result.Should().NotBeNull();
        result.Agent.Should().NotBeNull();

        result.AllowedToolNames.Should().BeEquivalentTo(
            "pf_get_accounts", "pf_list_invoices", "pf_search_payees");

        result.RemovedToolNames.Should().BeEquivalentTo(
            "pf_create_invoice", "pf_update_account", "pf_apply_proposal", "user_memory_recall");

        descriptor.LastBuildAllowedToolNames.Should().NotBeNull(
            "the builder must have called the descriptor's Build(allowedToolNames) overload");
        descriptor.LastBuildAllowedToolNames!.Should().BeEquivalentTo(result.AllowedToolNames);
    }

    [Fact]
    public void BuildReadOnlyVariant_With_AllReadOnly_Tools_Removes_Nothing()
    {
        var descriptor = new FakeDomainAgentDescriptor(
            name: "read-only-agent",
            toolNames: new[] { "pf_get_x", "pf_list_y", "pf_describe_z" });

        var services = BuildServiceProvider();
        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector());

        var result = builder.BuildReadOnlyVariant(descriptor, services);

        result.AllowedToolNames.Should().HaveCount(3);
        result.RemovedToolNames.Should().BeEmpty();
    }

    [Fact]
    public void BuildReadOnlyVariant_With_AllMutating_Tools_Removes_Everything()
    {
        var descriptor = new FakeDomainAgentDescriptor(
            name: "fully-mutating-agent",
            toolNames: new[] { "pf_create_x", "pf_delete_y", "pf_apply_z" });

        var services = BuildServiceProvider();
        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector());

        var result = builder.BuildReadOnlyVariant(descriptor, services);

        result.AllowedToolNames.Should().BeEmpty();
        result.RemovedToolNames.Should().HaveCount(3);
    }

    [Fact]
    public void BuildReadOnlyVariant_With_NoTools_Returns_Empty_Allowed()
    {
        var descriptor = new FakeDomainAgentDescriptor(
            name: "no-tools-agent",
            toolNames: Array.Empty<string>());

        var services = BuildServiceProvider();
        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector());

        var result = builder.BuildReadOnlyVariant(descriptor, services);

        result.AllowedToolNames.Should().BeEmpty();
        result.RemovedToolNames.Should().BeEmpty();
    }

    [Fact]
    public void BuildReadOnlyVariant_Should_Throw_On_Null_Descriptor()
    {
        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector());

        var act = () => builder.BuildReadOnlyVariant(null!, BuildServiceProvider());

        act.Should().Throw<ArgumentNullException>();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IChatClient, FakeChatClient>();
        return services.BuildServiceProvider();
    }

    // ── Test doubles ───────────────────────────────────────────────────────

    private sealed class FakeDomainAgentDescriptor : IDomainAgentDescriptor
    {
        private readonly IReadOnlyList<string> _toolNames;
        public IReadOnlySet<string>? LastBuildAllowedToolNames { get; private set; }

        public FakeDomainAgentDescriptor(string name, IReadOnlyList<string> toolNames)
        {
            Name = name;
            _toolNames = toolNames;
        }

        public string Name { get; }
        public string Description => $"Fake descriptor for {Name}";

        public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
            => Build(chatClient, serviceProvider, instructionsOverride: null, allowedToolNames: null);

        public AIAgent Build(
            IChatClient chatClient,
            IServiceProvider serviceProvider,
            string? instructionsOverride,
            IReadOnlySet<string>? allowedToolNames)
        {
            LastBuildAllowedToolNames = allowedToolNames;
            // Returning a real ChatClientAgent ensures the test agent type
            // matches what the production builder produces, so any
            // type-narrowing checks downstream still pass.
            return new ChatClientAgent(chatClient, name: Name, instructions: instructionsOverride);
        }

        public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider) => _toolNames;
    }

    private sealed class FakeChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Test fake");

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Test fake");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
