using Aonik.Ai.Middleware;
using Aonik.Ai.Observability;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Ai;

/// <summary>
/// Regression tests for <see cref="AuditMiddleware"/>'s use_case resolution.
///
/// Prior to the fix, when no <c>aonik.use_case</c> property was stamped on the
/// <see cref="ChatOptions"/>, AuditMiddleware would fall back to
/// <c>options.ModelId</c> and stamp THAT as the use_case. Because AuditMiddleware
/// mutates <c>options.AdditionalProperties</c> in place, the corrupted value
/// then bled back up to <see cref="TelemetryChatClient"/> (which re-reads the
/// options after the inner call), causing model ids like <c>gpt-5-nano</c> to
/// appear as the <c>traceName</c> in the AiTraceObservation log line — and as
/// the displayed trace name in the observability UI's trace list.
///
/// These tests pin the fix: the resolver returns the generic <c>"chat"</c>
/// bucket when no stamp is present, regardless of whether ModelId is set.
/// </summary>
public class AuditMiddlewareUseCaseFallbackTests
{
    [Fact]
    public async Task GetResponseAsync_ShouldStampDefaultUseCase_WhenNoUseCaseProvidedAndModelIdSet()
    {
        var inner = new StubChatClient();
        var aiRunWriter = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, aiRunWriter, NullLogger<AuditMiddleware>.Instance);

        // No aonik.use_case stamped — only a ModelId. Pre-fix this would have
        // leaked "gpt-5-nano" as the use_case.
        var options = new ChatOptions { ModelId = "gpt-5-nano" };

        await sut.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }, options);

        aiRunWriter.RecordedUseCase.Should().Be("chat");
        options.AdditionalProperties.Should().NotBeNull();
        options.AdditionalProperties![TelemetryChatClient.UseCasePropertyKey].Should().Be("chat");
    }

    [Fact]
    public async Task GetResponseAsync_ShouldStampDefaultUseCase_WhenOptionsAreNull()
    {
        var inner = new StubChatClient();
        var aiRunWriter = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, aiRunWriter, NullLogger<AuditMiddleware>.Instance);

        await sut.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }, options: null);

        aiRunWriter.RecordedUseCase.Should().Be("chat");
    }

    [Fact]
    public async Task GetResponseAsync_ShouldPreserveStampedUseCase_WhenProvided()
    {
        var inner = new StubChatClient();
        var aiRunWriter = new RecordingAiRunWriter();
        var sut = new AuditMiddleware(inner, aiRunWriter, NullLogger<AuditMiddleware>.Instance);

        var options = new ChatOptions { ModelId = "gpt-5-nano" };
        options.AdditionalProperties = new AdditionalPropertiesDictionary
        {
            [AiTelemetry.UseCaseAttribute] = "title-generation",
        };

        await sut.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }, options);

        aiRunWriter.RecordedUseCase.Should().Be("title-generation");
        options.AdditionalProperties[TelemetryChatClient.UseCasePropertyKey].Should().Be("title-generation");
    }

    [Fact]
    public async Task GetResponseAsync_ShouldNeverLeakModelIdAsUseCase()
    {
        // Hard guard against the regression — exercise a few common model ids
        // that have appeared as confusing trace names in production.
        foreach (var modelId in new[] { "gpt-5-nano", "gpt-5.4-nano", "gpt-5-mini", "gpt-4o-mini" })
        {
            var inner = new StubChatClient();
            var aiRunWriter = new RecordingAiRunWriter();
            var sut = new AuditMiddleware(inner, aiRunWriter, NullLogger<AuditMiddleware>.Instance);

            var options = new ChatOptions { ModelId = modelId };

            await sut.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") }, options);

            aiRunWriter.RecordedUseCase.Should().NotBe(modelId,
                because: "ModelId is a 'what' (provider/model), not a 'why' (semantic use_case)");
            options.AdditionalProperties.Should().NotBeNull();
            options.AdditionalProperties![TelemetryChatClient.UseCasePropertyKey].Should().NotBe(modelId);
        }
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ChatResponse([new ChatMessage(ChatRole.Assistant, "ok")]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return EmptyStream();

            static async IAsyncEnumerable<ChatResponseUpdate> EmptyStream()
            {
                await Task.CompletedTask;
                yield break;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class RecordingAiRunWriter : IAiRunWriter
    {
        public string? RecordedUseCase { get; private set; }

        public Task<Guid> StartRunAsync(string useCase, string inputRefsJson, CancellationToken cancellationToken = default)
        {
            RecordedUseCase = useCase;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task MarkRunCompletedAsync(Guid aiRunId, string? outputRef = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkRunCompletedWithMetricsAsync(
            Guid aiRunId,
            int tokensUsed,
            int latencyMs,
            decimal costEstimate,
            string? outputRef = null,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task MarkRunFailedAsync(Guid aiRunId, string failureReason, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<Guid> SaveRunAsync(string useCase, string inputRefsJson, string outcome, CancellationToken cancellationToken = default)
        {
            RecordedUseCase = useCase;
            return Task.FromResult(Guid.NewGuid());
        }
    }
}
