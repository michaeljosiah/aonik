using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.Voice.Tools;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Voice.Tests.Tools;

public class VoiceAgentBuilderTests
{
    /// <summary>A gate with no classifications — every read-only variant test behaves as before.</summary>
    private static VoiceAgentBuilder NewBuilder() =>
        new(new NamingPrefixVoiceToolSafetyInspector(), new FakeToolApprovalGate());

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
        var builder = NewBuilder();

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
        var builder = NewBuilder();

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
        var builder = NewBuilder();

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
        var builder = NewBuilder();

        var result = builder.BuildReadOnlyVariant(descriptor, services);

        result.AllowedToolNames.Should().BeEmpty();
        result.RemovedToolNames.Should().BeEmpty();
    }

    [Fact]
    public void BuildReadOnlyVariant_Should_Throw_On_Null_Descriptor()
    {
        var builder = NewBuilder();

        var act = () => builder.BuildReadOnlyVariant(null!, BuildServiceProvider());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildVariant_With_Classified_Mutating_Tools_Builds_Full_Gated_Agent()
    {
        // Spec 032 — when the agent's mutating tools are classified, voice exposes the FULL toolset
        // (the descriptor's gate wraps the mutations) so Medium/High can be approved over voice.
        var descriptor = new FakeDomainAgentDescriptor(
            name: "finance-agent",
            toolNames: new[] { "finance_get_invoice", "finance_create_invoice", "finance_capture_payment" });

        var gate = new FakeToolApprovalGate()
            .WithReadOnly("finance_get_invoice")
            .WithMutating("finance_create_invoice", ToolApprovalTier.Medium)
            .WithMutating("finance_capture_payment", ToolApprovalTier.High);

        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector(), gate);

        var result = builder.BuildVariant(descriptor, BuildServiceProvider());

        result.ToolMode.Should().Be(VoiceAgentToolMode.Gated);
        result.AllowedToolNames.Should().BeEquivalentTo(
            "finance_get_invoice", "finance_create_invoice", "finance_capture_payment");
        result.RemovedToolNames.Should().BeEmpty();
        // The full (unfiltered) Build overload is used — no allow-list applied.
        descriptor.LastBuildAllowedToolNames.Should().BeNull();
    }

    [Fact]
    public void BuildVariant_With_Unclassified_Mutating_Tool_Falls_Back_To_ReadOnly()
    {
        // An unclassified mutating-looking tool would make the gate fail closed on the full toolset,
        // so the builder falls back to the read-only subset rather than exposing it.
        var descriptor = new FakeDomainAgentDescriptor(
            name: "mixed-agent",
            toolNames: new[] { "pf_get_accounts", "pf_create_invoice" });

        // Gate classifies nothing → pf_create_invoice is an unclassified mutation.
        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector(), new FakeToolApprovalGate());

        var result = builder.BuildVariant(descriptor, BuildServiceProvider());

        result.ToolMode.Should().Be(VoiceAgentToolMode.ReadOnly);
        result.AllowedToolNames.Should().BeEquivalentTo("pf_get_accounts");
        result.RemovedToolNames.Should().Contain("pf_create_invoice");
    }

    [Fact]
    public void BuildVariant_With_Only_ReadOnly_Tools_Falls_Back_To_ReadOnly()
    {
        // No classified mutation → nothing to gate → read-only variant (which keeps every read tool).
        var descriptor = new FakeDomainAgentDescriptor(
            name: "read-only-agent",
            toolNames: new[] { "pf_get_x", "pf_list_y" });

        var builder = new VoiceAgentBuilder(new NamingPrefixVoiceToolSafetyInspector(), new FakeToolApprovalGate());

        var result = builder.BuildVariant(descriptor, BuildServiceProvider());

        result.ToolMode.Should().Be(VoiceAgentToolMode.ReadOnly);
        result.AllowedToolNames.Should().BeEquivalentTo("pf_get_x", "pf_list_y");
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

    /// <summary>
    /// Gate double whose only meaningful behaviour is <see cref="Classify"/> — that is all
    /// <see cref="VoiceAgentBuilder.BuildVariant"/> consults. Gate/GateAll pass tools through, since
    /// the test descriptor's Build doesn't actually wrap anything.
    /// </summary>
    private sealed class FakeToolApprovalGate : IToolApprovalGate
    {
        private readonly Dictionary<string, ToolClassification> _map = new(StringComparer.OrdinalIgnoreCase);

        public FakeToolApprovalGate WithReadOnly(string name)
        {
            _map[name] = ToolClassification.ReadOnly;
            return this;
        }

        public FakeToolApprovalGate WithMutating(string name, ToolApprovalTier tier)
        {
            _map[name] = ToolClassification.Mutating(new ToolApprovalOptions(tier, ActionKind: name));
            return this;
        }

        public AITool Gate(AITool tool, IServiceProvider? serviceProvider = null) => tool;

        public IEnumerable<AITool> GateAll(IEnumerable<AITool> tools, IServiceProvider? serviceProvider = null) => tools;

        public ToolClassification? Classify(string toolName) =>
            _map.TryGetValue(toolName, out var classification) ? classification : null;
    }
}
