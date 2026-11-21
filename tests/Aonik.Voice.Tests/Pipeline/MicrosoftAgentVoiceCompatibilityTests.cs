using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Voxa.Frames;
using Voxa.Processors;
using Voxa.Services.MicrosoftAgents;

using Aonik.Agents.Contracts.Agui;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Agents;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.Voice.Frames;
using Aonik.Voice.Processors;

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
        List<Frame> frames = await sink.ReadUntilAsync<LlmTurnEndedFrame>(timeout.Token);

        // Assert
        frames.OfType<LlmTurnStartedFrame>().Should().ContainSingle();
        frames.OfType<LlmTextChunkFrame>().Select(f => f.Text).Should().Equal("Ready for review.");
        frames.OfType<LlmTurnEndedFrame>().Should().ContainSingle();
        client.LastOptions.Should().NotBeNull();
        client.LastOptions!.ModelId.Should().Be("configured-route-model");
        client.LastUserMessage.Should().Be("invoice status");

        await processor.QueueFrameAsync(new EndFrame(), timeout.Token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VoiceApproval_Should_HandOffWhenConfirmationOrTheExactPreviewCannotBeShown(bool advertisesConfirmation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var client = new CapturingChatClient();
        var agent = new ChatClientAgent(client, name: "voice-review");
        Guid requestId = Guid.NewGuid();
        var services = new ReviewTurnServices();
        HashSet<string> frontendTools = new(StringComparer.OrdinalIgnoreCase);
        if (advertisesConfirmation)
        {
            frontendTools.Add("confirmAction");
            services.Record(ToolApprovalQueuedResult.For("gmail_send_message", new(ToolApprovalTier.High, "Send mail"), Guid.NewGuid(), requestId)
                with { Preview = new("Review the exact recipients", null, JsonSerializer.SerializeToElement(new { recipient = "fixture@example.test" })) });
        }
        else
        {
            services.Record(ToolApprovalRequiredResult.For("finance_issue_invoice", new(ToolApprovalTier.Medium, "Issue invoice"), requestId));
        }

        await using var processor = AonikVoiceAgent.CreateProcessor(agent, null, null, frontendTools, services, services, services,
            services, services, null, null, null, null, adminUiBaseUrl: "https://admin.example.test");
        await using var sink = new CaptureSink();
        processor.Link(sink);
        sink.Start(timeout.Token);
        processor.Start(timeout.Token);

        await processor.QueueFrameAsync(new StartFrame(), timeout.Token);
        await processor.QueueFrameAsync(new TranscriptionFrame("prepare the action", true, null, null), timeout.Token);
        List<Frame> frames = await sink.ReadUntilAsync<ApprovalReviewFrame>(timeout.Token);

        frames.OfType<ToolCallRequestFrame>().Should().BeEmpty();
        services.DecisionCalls.Should().Be(0);
        client.LastOptions!.AdditionalProperties![AiTelemetry.UseCaseAttribute].Should().Be("voice");
        client.LastOptions.AdditionalProperties[AiTelemetry.ConversationThreadIdProperty].Should().Be(services.ThreadId);
        var handoff = frames.OfType<ApprovalReviewFrame>().Should().ContainSingle().Which;
        using var envelope = JsonDocument.Parse(ThreadReadyFrameSerializer.Serialize(handoff)!);
        envelope.RootElement.GetProperty("type").GetString().Should().Be("approvalReview");
        envelope.RootElement.GetProperty("reviewPath").GetString().Should().Be($"/approvals?requestId={requestId:D}");
        envelope.RootElement.GetProperty("reviewUrl").GetString().Should().Be($"https://admin.example.test/approvals?requestId={requestId:D}");
        envelope.RootElement.GetRawText().Should().NotContain("fixture@example.test");

        await processor.QueueFrameAsync(new EndFrame(), timeout.Token);
    }

    private sealed class ReviewTurnServices : IChatThreadManager, IAguiMessageConverter, IPostStreamPersistenceCoordinator,
        IToolApprovalStreamNotifier, IToolApprovalService
    {
        private readonly List<object> _pending = [];
        public int DecisionCalls { get; private set; }
        public Guid ThreadId { get; } = Guid.NewGuid();

        public Task<ChatThreadContext> EnsureThreadAsync(string? clientThreadId, IReadOnlyList<AguiMessage>? messages, string? agentId,
            CancellationToken cancellationToken) => Task.FromResult(new ChatThreadContext(ThreadId, ThreadId.ToString("N"), true, messages?.Last().Content));

        public Task<ChatHistoryResolution> ReconstructHistoryAsync(Guid? persistedThreadId, IReadOnlyList<AguiMessage>? clientMessages,
            CancellationToken cancellationToken) => Task.FromResult(new ChatHistoryResolution(clientMessages, "client", 0));

        public List<ChatMessage> ConvertMessages(IEnumerable<AguiMessage>? messages) =>
            messages?.Select(message => new ChatMessage(ChatRole.User, message.Content)).ToList() ?? [];

        public List<AITool> ConvertClientTools(List<JsonElement>? toolElements) => [];
        public void Enqueue(PostStreamPersistenceContext context) { }
        public void Record(object approvalResult) => _pending.Add(approvalResult);
        public IReadOnlyList<object> Drain()
        {
            object[] results = _pending.ToArray();
            _pending.Clear();
            return results;
        }

        public Task<ToolGateOutcome> GateAsync(ToolGateContext context, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<ToolApprovalDecisionResult> DecideAsync(Guid approvalRequestId, ToolApprovalDecisionInput decision, CancellationToken cancellationToken = default)
        {
            DecisionCalls++;
            throw new InvalidOperationException("The voice handoff must leave the request pending.");
        }
    }

    private sealed class CaptureSink() : FrameProcessor("MafCompatibilitySink")
    {
        private readonly Channel<Frame> _frames = Channel.CreateUnbounded<Frame>();

        protected override ValueTask ProcessFrameAsync(Frame frame, CancellationToken ct)
            => _frames.Writer.WriteAsync(frame, ct);

        public async Task<List<Frame>> ReadUntilAsync<TFrame>(CancellationToken cancellationToken = default) where TFrame : Frame
        {
            var frames = new List<Frame>();
            await foreach (Frame frame in _frames.Reader.ReadAllAsync(cancellationToken))
            {
                frames.Add(frame);
                if (frame is TFrame)
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
