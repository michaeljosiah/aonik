using Aonik.Ai.Observability;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Tests that the TelemetryChatClient decorator emits the canonical
/// <c>AiCallCompleted</c> audit log and the new <c>AiTraceObservation</c>
/// log line, for both buffered and streaming responses, and on success and
/// failure paths.
/// </summary>
public class TelemetryChatClientTests
{
    [Fact]
    public async Task GetResponseAsync_ShouldEmitAiCallCompleted_WithUseCaseAndTokens()
    {
        var inner = new FakeChatClient(response: BuildResponse(input: 100, output: 50, model: "gpt-4o-mini"));
        var capture = new CapturingLogger<TelemetryChatClient>();
        var sut = new TelemetryChatClient(inner, capture);

        var options = new ChatOptions { ModelId = "gpt-4o-mini" };
        options.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [TelemetryChatClient.UseCasePropertyKey] = "conversation.summary"
        };

        var response = await sut.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hello") }, options);

        response.Should().NotBeNull();
        capture.Entries.Should().Contain(e => e.Message.StartsWith("AiCallCompleted"));
        capture.Entries.Should().Contain(e => e.Message.StartsWith("AiTraceObservation"));

        var entry = capture.Entries.Single(e => e.Message.StartsWith("AiCallCompleted"));
        entry.State.Should().Contain(kv => kv.Key == "UseCase" && (string)kv.Value! == "conversation.summary");
        entry.State.Should().Contain(kv => kv.Key == "Outcome" && (string)kv.Value! == "success");
        entry.State.Should().Contain(kv => kv.Key == "InputTokens" && (int)kv.Value! == 100);
        entry.State.Should().Contain(kv => kv.Key == "OutputTokens" && (int)kv.Value! == 50);
        entry.State.Should().Contain(kv => kv.Key == "TotalTokens" && (int)kv.Value! == 150);
        entry.State.Should().Contain(kv => kv.Key == "EstimatedCostUsd" && (double)kv.Value! > 0);
    }

    [Fact]
    public async Task GetResponseAsync_ShouldEmitErrorEntry_WhenInnerThrows()
    {
        var inner = new FakeChatClient(throws: new InvalidOperationException("model unavailable"));
        var capture = new CapturingLogger<TelemetryChatClient>();
        var sut = new TelemetryChatClient(inner, capture);

        var act = () => sut.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        await act.Should().ThrowAsync<InvalidOperationException>();
        capture.Entries.Should().Contain(e => e.Message.StartsWith("AiCallCompleted"));
        capture.Entries.Should().Contain(e => e.Message.StartsWith("AiTraceObservation"));

        var entry = capture.Entries.Single(e => e.Message.StartsWith("AiCallCompleted"));
        entry.Message.Should().StartWith("AiCallCompleted");
        entry.State.Should().Contain(kv => kv.Key == "Outcome" && (string)kv.Value! == "error");
        entry.LogLevel.Should().Be(LogLevel.Warning);
    }

    [Fact]
    public async Task GetStreamingResponseAsync_ShouldEmitOneEntry_AfterStreamCompletes()
    {
        var inner = new FakeChatClient(streamingChunks: new[]
        {
            new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("Hel")] },
            new ChatResponseUpdate { Role = ChatRole.Assistant, Contents = [new TextContent("lo")] },
            new ChatResponseUpdate
            {
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 2,
                    TotalTokenCount = 12,
                })],
            },
        });
        var capture = new CapturingLogger<TelemetryChatClient>();
        var sut = new TelemetryChatClient(inner, capture);

        var collected = new List<ChatResponseUpdate>();
        await foreach (var update in sut.GetStreamingResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }))
        {
            collected.Add(update);
        }

        collected.Should().HaveCount(3);
        capture.Entries.Should().Contain(e => e.Message.StartsWith("AiCallCompleted"));
        capture.Entries.Should().Contain(e => e.Message.StartsWith("AiTraceObservation"));

        var entry = capture.Entries.Single(e => e.Message.StartsWith("AiCallCompleted"));
        entry.State.Should().Contain(kv => kv.Key == "Operation" && (string)kv.Value! == "chat.stream");
        entry.State.Should().Contain(kv => kv.Key == "InputTokens" && (int)kv.Value! == 10);
        entry.State.Should().Contain(kv => kv.Key == "OutputTokens" && (int)kv.Value! == 2);
    }

    private static ChatResponse BuildResponse(int input, int output, string model)
    {
        return new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")])
        {
            ModelId = model,
            Usage = new UsageDetails
            {
                InputTokenCount = input,
                OutputTokenCount = output,
                TotalTokenCount = input + output,
            },
        };
    }

    private sealed class FakeChatClient : IChatClient
    {
        private readonly ChatResponse? _response;
        private readonly Exception? _throws;
        private readonly ChatResponseUpdate[]? _streamingChunks;

        public FakeChatClient(
            ChatResponse? response = null,
            Exception? throws = null,
            ChatResponseUpdate[]? streamingChunks = null)
        {
            _response = response;
            _throws = throws;
            _streamingChunks = streamingChunks;
        }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            if (_throws is not null) throw _throws;
            return Task.FromResult(_response ?? new ChatResponse([new ChatMessage(ChatRole.Assistant, "")]));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (_throws is not null) throw _throws;
            foreach (var chunk in _streamingChunks ?? Array.Empty<ChatResponseUpdate>())
            {
                await Task.Yield();
                yield return chunk;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed record CapturedEntry(LogLevel LogLevel, string Message, IReadOnlyList<KeyValuePair<string, object?>> State);

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<CapturedEntry> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var props = state as IReadOnlyList<KeyValuePair<string, object?>> ?? Array.Empty<KeyValuePair<string, object?>>();
            Entries.Add(new CapturedEntry(logLevel, message, props));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }
}
