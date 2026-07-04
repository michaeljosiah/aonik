using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Verifies the load-bearing assumption behind the H14 double-write fix: a marker set on
/// <see cref="ChatClientAgentRunOptions"/>.<c>ChatOptions.AdditionalProperties</c> actually
/// reaches the inner <see cref="IChatClient"/> the audit middleware wraps. If MAF rebuilt
/// ChatOptions and dropped AdditionalProperties, the AG-UI/voice suppression marker would never
/// arrive and those paths would still double-write — so this pins the behaviour.
/// </summary>
public class RunOptionsAdditionalPropertiesFlowTests
{
    [Fact]
    public async Task RunStreamingAsync_Should_FlowRunOptionsAdditionalProperties_ToInnerClient()
    {
        var capturing = new OptionsCapturingChatClient();
        var agent = new ChatClientAgent(capturing, name: "test-agent", instructions: "You are a test.");

        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [AiTelemetry.StreamAuditHandledDownstreamAttribute] = true,
            },
        });

        await foreach (var _ in agent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "hi")], session: null, options: runOptions))
        {
        }

        capturing.LastStreamingOptions.Should().NotBeNull();
        capturing.LastStreamingOptions!.AdditionalProperties.Should().NotBeNull();
        capturing.LastStreamingOptions.AdditionalProperties!
            .TryGetValue(AiTelemetry.StreamAuditHandledDownstreamAttribute, out var value)
            .Should().BeTrue("the marker set on the run options must reach the inner IChatClient");
        value.Should().Be(true);
    }

    private sealed class OptionsCapturingChatClient : IChatClient
    {
        public ChatOptions? LastStreamingOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            LastStreamingOptions = options;
            return Stream();

            static async IAsyncEnumerable<ChatResponseUpdate> Stream()
            {
                await Task.CompletedTask;
                yield return new ChatResponseUpdate(ChatRole.Assistant, "ok");
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
