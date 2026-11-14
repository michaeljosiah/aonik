using System.Runtime.CompilerServices;
using System.Threading.Channels;

using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Voxa.Frames;
using Voxa.Processors;
using Voxa.Services.MicrosoftAgents;

namespace Aonik.Voice.Tests.Pipeline;

public class MicrosoftAgentVoiceCompatibilityTests
{
    [Fact]
    public async Task VoiceAdapter_Should_StreamTheMafResponse_WithConfiguredRunOptions()
    {
        // Arrange
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new CapturingChatClient();
        var agent = new ChatClientAgent(client, name: "voice-compatibility");
        var turnCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var processor = MicrosoftAgentVoice.CreateProcessor(agent, options =>
        {
            options.BuildRunOptions = _ => new ChatClientAgentRunOptions(new ChatOptions
            {
                ModelId = "configured-route-model",
            });
            options.OnTurnCompleted = (_, _, _) =>
            {
                turnCompleted.TrySetResult();
                return ValueTask.CompletedTask;
            };
            options.OnTurnFailed = (_, error, _) =>
            {
                turnCompleted.TrySetException(error);
                return ValueTask.CompletedTask;
            };
        });
        await using var sink = new CaptureSink();
        processor.Link(sink);
        sink.Start(timeout.Token);
        processor.Start(timeout.Token);

        // Act
        await processor.QueueFrameAsync(new StartFrame(), timeout.Token);
        await processor.QueueFrameAsync(new TranscriptionFrame("invoice status", true, null, null), timeout.Token);
        await turnCompleted.Task.WaitAsync(timeout.Token);
        List<Frame> frames = await sink.ReadTurnAsync(timeout.Token);

        // Assert
        frames.OfType<LlmTurnStartedFrame>().Should().ContainSingle();
        frames.OfType<LlmTextChunkFrame>().Select(f => f.Text).Should().Equal("Ready for review.");
        frames.OfType<LlmTurnEndedFrame>().Should().ContainSingle();
        client.LastOptions.Should().NotBeNull();
        client.LastOptions!.ModelId.Should().Be("configured-route-model");
        client.LastUserMessage.Should().Be("invoice status");

        await processor.QueueFrameAsync(new EndFrame(), timeout.Token);
    }

    private sealed class CaptureSink() : FrameProcessor("MafCompatibilitySink")
    {
        private readonly Channel<Frame> _frames = Channel.CreateUnbounded<Frame>();

        protected override ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
            => _frames.Writer.WriteAsync(frame, ct);

        public async Task<List<Frame>> ReadTurnAsync(CancellationToken cancellationToken = default)
        {
            var frames = new List<Frame>();
            await foreach (Frame frame in _frames.Reader.ReadAllAsync(cancellationToken))
            {
                frames.Add(frame);
                if (frame is LlmTurnEndedFrame)
                {
                    return frames;
                }
            }

            return frames;
        }
    }

    private sealed class CapturingChatClient : IChatClient
    {
        public ChatOptions? LastOptions { get; private set; }
        public string? LastUserMessage { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("The voice adapter must use streaming.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            LastUserMessage = messages.Last(m => m.Role == ChatRole.User).Text;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Ready for review.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
