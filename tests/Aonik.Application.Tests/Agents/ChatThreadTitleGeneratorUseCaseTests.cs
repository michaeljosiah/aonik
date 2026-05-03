using Aonik.Agents.Framework;
using Aonik.SharedKernel.Abstractions.Ai;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aonik.Application.Tests.Agents;

/// <summary>
/// Regression test covering the half of the use_case fix that lives at the
/// caller. <see cref="ChatThreadTitleGenerator"/> must stamp
/// <c>aonik.use_case = "title-generation"</c> on the <see cref="ChatOptions"/>
/// it passes to <see cref="IChatClient.GetResponseAsync"/>; otherwise the
/// AiTraceObservation row for this call carries no semantic name and the
/// trace explorer's dedupe heuristic can pick it as the representative for
/// the parent run, displaying the model id (e.g. <c>gpt-5-nano</c>) as the
/// trace name instead of something useful.
/// </summary>
public class ChatThreadTitleGeneratorUseCaseTests
{
    [Fact]
    public async Task GenerateTitleAsync_ShouldStampTitleGenerationUseCase_OnChatOptions()
    {
        var capturingClient = new OptionsCapturingChatClient(
            new ChatResponse([new ChatMessage(ChatRole.Assistant, "A short title")]));

        var resolver = new StubProfileResolver(new AiTaskProfile(
            ModelId: "gpt-5-nano",
            SystemPrompt: "Generate a concise title.",
            UserPromptTemplate: null));

        var sut = new ChatThreadTitleGenerator(
            capturingClient,
            resolver,
            NullLogger<ChatThreadTitleGenerator>.Instance);

        await sut.GenerateTitleAsync("How do I budget for the holidays?");

        capturingClient.LastOptions.Should().NotBeNull();
        capturingClient.LastOptions!.AdditionalProperties.Should().NotBeNull();
        capturingClient.LastOptions.AdditionalProperties!
            .TryGetValue(AiTelemetry.UseCaseAttribute, out var stamped)
            .Should().BeTrue();
        stamped.Should().Be("title-generation",
            because: "the title generator's call must be tagged with a semantic use_case so the trace listing shows 'title-generation' instead of leaking the model id");
        capturingClient.LastOptions.ModelId.Should().Be("gpt-5-nano");
    }

    private sealed class OptionsCapturingChatClient : IChatClient
    {
        private readonly ChatResponse _response;

        public OptionsCapturingChatClient(ChatResponse response)
        {
            _response = response;
        }

        public ChatOptions? LastOptions { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            return Task.FromResult(_response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
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

    private sealed class StubProfileResolver : IAiTaskProfileResolver
    {
        private readonly AiTaskProfile _profile;

        public StubProfileResolver(AiTaskProfile profile)
        {
            _profile = profile;
        }

        public Task<AiTaskProfile> ResolveAsync(
            string useCase,
            string? promptName = null,
            string? defaultModelId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_profile);
    }
}
