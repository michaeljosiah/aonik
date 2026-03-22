using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Generates a concise thread title by asking the LLM to summarise the
/// user's initial prompt. Falls back to truncation on any failure so that
/// title generation never blocks the primary chat flow.
///
/// The model used for title generation is resolved via <see cref="IAiModelResolver"/>
/// using the "title-generation" use-case key. If no policy is configured, falls
/// back to <see cref="DefaultTitleModelId"/>.
/// </summary>
internal sealed class ChatThreadTitleGenerator : IChatThreadTitleGenerator
{
    private readonly IChatClient _chatClient;
    private readonly IAiModelResolver _modelResolver;
    private readonly ILogger<ChatThreadTitleGenerator> _logger;
    private const string DefaultTitleModelId = "gpt-5-nano";
    private const string TitleGenerationUseCase = "title-generation";

    private const string SystemPrompt =
        """
        You are a title generator. Given a user message from a chat conversation,
        produce a short, descriptive title (maximum 8 words) that captures the
        intent of the message. Return ONLY the title text — no quotes, no
        punctuation wrapping, no explanation.
        """;

    public ChatThreadTitleGenerator(
        IChatClient chatClient,
        IAiModelResolver modelResolver,
        ILogger<ChatThreadTitleGenerator> logger)
    {
        _chatClient = chatClient;
        _modelResolver = modelResolver;
        _logger = logger;
    }

    public async Task<string> GenerateTitleAsync(
        string firstUserMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve model from AiRoutePolicy; fall back to default if not configured
            var modelId = await _modelResolver.ResolveModelNameAsync(
                TitleGenerationUseCase, cancellationToken)
                ?? DefaultTitleModelId;

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, firstUserMessage),
            };

            var response = await _chatClient.GetResponseAsync(
                messages,
                options: new ChatOptions
                {
                    ModelId = modelId,
                },
                cancellationToken: cancellationToken);

            var title = response.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(title))
            {
                // Strip any wrapping quotes the model might add
                title = title.Trim('"', '\'', '\u201c', '\u201d');

                // Enforce max length
                if (title.Length > 200)
                    title = title[..197] + "...";

                _logger.LogDebug(
                    "Generated thread title: {Title} (model: {ModelId})", title, modelId);

                return title;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to generate thread title via LLM — falling back to truncation");
        }

        // Fallback: truncate the first message
        return firstUserMessage.Length > 60
            ? firstUserMessage[..57] + "..."
            : firstUserMessage;
    }
}
