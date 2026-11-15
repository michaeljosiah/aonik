using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Verifies that MAF passes the server-resolved conversation thread to the chat client audit.
/// </summary>
public class RunOptionsAdditionalPropertiesFlowTests
{
    [Fact]
    public async Task RunStreamingAsync_Should_FlowRunOptionsAdditionalProperties_ToInnerClient()
    {
        var capturing = new OptionsCapturingChatClient();
        var agent = new ChatClientAgent(capturing, name: "test-agent", instructions: "You are a test.");
        Guid threadId = Guid.NewGuid();

        var runOptions = new ChatClientAgentRunOptions(new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [AiTelemetry.ConversationThreadIdProperty] = threadId,
            },
        });

        await foreach (var _ in agent.RunStreamingAsync(
            [new ChatMessage(ChatRole.User, "hi")], session: null, options: runOptions))
        {
        }

        capturing.LastStreamingOptions.Should().NotBeNull();
        capturing.LastStreamingOptions!.AdditionalProperties.Should().NotBeNull();
        capturing.LastStreamingOptions.AdditionalProperties!
            .TryGetValue(AiTelemetry.ConversationThreadIdProperty, out var value)
            .Should().BeTrue();
        value.Should().Be(threadId);
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
