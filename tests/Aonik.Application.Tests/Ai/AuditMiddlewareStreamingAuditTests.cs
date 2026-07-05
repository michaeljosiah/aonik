using System.Runtime.CompilerServices;
using Aonik.Ai.Middleware;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

// NOTE: AiTelemetry lives in Aonik.SharedKernel.Abstractions.Ai (imported above).

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Pins the H14 fix: streaming AI calls must get the same AiRun lifecycle as non-streaming
/// ones — a run is started, completed with token metrics on success, and marked failed
/// (with the fault rethrown) on error. Before the fix the streaming branch only logged.
/// </summary>
public class AuditMiddlewareStreamingAuditTests
{
    [Fact]
    public async Task GetStreamingResponseAsync_Should_StartAndCompleteRunWithTokens_When_StreamSucceeds()
    {
        // Two usage updates (running then final) — the recorded total must be the LAST value (42),
        // not the sum (52): streaming usage is cumulative/last-wins, not additive.
        var updates = new List<ChatResponseUpdate>
        {
            new(ChatRole.Assistant, "Hel"),
            new() { Contents = [new UsageContent(new UsageDetails { TotalTokenCount = 20 })] },
            new(ChatRole.Assistant, "lo"),
            new() { Contents = [new UsageContent(new UsageDetails { TotalTokenCount = 42 })] },
        };
        var inner = new StreamingStubChatClient(updates, throwAt: null);
        var writer = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, writer, NullLogger<AuditMiddleware>.Instance);

        var received = new List<ChatResponseUpdate>();
        await foreach (var u in sut.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
        {
            received.Add(u);
        }

        received.Should().HaveCount(4, "every update must still flow through to the consumer");
        writer.StartCount.Should().Be(1);
        writer.CompletedTokens.Should().Be(42, "the last (cumulative) usage total must be recorded, not the sum");
        writer.FailedReason.Should().BeNull();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_Should_NotAudit_When_HandledDownstream()
    {
        // AG-UI / voice mark the call handled-downstream (they persist their own AiRun), so the
        // middleware must pass the stream through WITHOUT starting a second run.
        var updates = new List<ChatResponseUpdate>
        {
            new(ChatRole.Assistant, "hi"),
            new() { Contents = [new UsageContent(new UsageDetails { TotalTokenCount = 10 })] },
        };
        var inner = new StreamingStubChatClient(updates, throwAt: null);
        var writer = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, writer, NullLogger<AuditMiddleware>.Instance);

        var options = new ChatOptions
        {
            AdditionalProperties = new AdditionalPropertiesDictionary
            {
                [AiTelemetry.StreamAuditHandledDownstreamAttribute] = true,
            },
        };

        var received = new List<ChatResponseUpdate>();
        await foreach (var u in sut.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")], options))
        {
            received.Add(u);
        }

        received.Should().HaveCount(2, "updates must still pass through unchanged");
        writer.StartCount.Should().Be(0, "the downstream owner audits; the middleware must not double-write");
        writer.CompletedTokens.Should().BeNull();
    }

    [Fact]
    public async Task GetStreamingResponseAsync_Should_MarkFailedAndRethrow_When_StreamThrows()
    {
        var updates = new List<ChatResponseUpdate> { new(ChatRole.Assistant, "partial") };
        var boom = new InvalidOperationException("provider exploded");
        var inner = new StreamingStubChatClient(updates, throwAt: boom);
        var writer = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, writer, NullLogger<AuditMiddleware>.Instance);

        var act = async () =>
        {
            await foreach (var _ in sut.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
            {
            }
        };

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("provider exploded");
        writer.StartCount.Should().Be(1);
        writer.FailedReason.Should().Be("provider exploded", "the fault must be recorded on the AiRun");
        writer.CompletedTokens.Should().BeNull("a failed stream must not also be marked completed");
    }

    [Fact]
    public async Task GetResponseAsync_Should_CompleteWithStructuredTokenMetrics()
    {
        // H17: the non-streaming path must record structured token metrics (via
        // MarkRunCompletedWithMetricsAsync), not an unstructured outputRef string.
        var response = new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
        {
            Usage = new UsageDetails { TotalTokenCount = 30 },
        };
        var inner = new RespondingChatClient(response);
        var writer = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, writer, NullLogger<AuditMiddleware>.Instance);

        await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        writer.StartCount.Should().Be(1);
        writer.CompletedTokens.Should().Be(30, "the non-streaming completion must record the token count");
    }

    private sealed class RespondingChatClient(ChatResponse response) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Task.FromResult(response);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class StreamingStubChatClient(IReadOnlyList<ChatResponseUpdate> updates, Exception? throwAt) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => Stream();

        private async IAsyncEnumerable<ChatResponseUpdate> Stream([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var update in updates)
            {
                await Task.Yield();
                yield return update;
            }

            if (throwAt is not null)
            {
                throw throwAt;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class RecordingAiRunWriter : IAiRunWriter
    {
        public int StartCount { get; private set; }
        public int? CompletedTokens { get; private set; }
        public string? FailedReason { get; private set; }

        public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default)
        {
            StartCount++;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkRunCompletedWithMetricsAsync(
            Guid aiRunId, int tokensUsed, int latencyMs, decimal costEstimate,
            string? outputRef = null, CancellationToken cancellationToken = default)
        {
            CompletedTokens = tokensUsed;
            return Task.CompletedTask;
        }

        public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default)
        {
            FailedReason = failureReason;
            return Task.CompletedTask;
        }

        public Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());
    }
}
