using Aonik.Agents.Contracts.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Generates a concise thread title by asking the LLM to summarise the
/// user's initial prompt. Falls back to truncation on any failure so that
/// title generation never blocks the primary chat flow.
/// </summary>
internal sealed class ChatThreadTitleGenerator : IChatThreadTitleGenerator
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ChatThreadTitleGenerator> _logger;
    private const string TitleGenerationModelId = "gpt-5-nano";

    private const string SystemPrompt =
        """
        You are a title generator. Given a user message from a chat conversation,
        produce a short, descriptive title (maximum 8 words) that captures the
        intent of the message. Return ONLY the title text — no quotes, no
        punctuation wrapping, no explanation.
        """;

    public ChatThreadTitleGenerator(
        IChatClient chatClient,
        ILogger<ChatThreadTitleGenerator> logger)
    {
        _chatClient = chatClient;
        _logger = logger;
    }

    public async Task<string> GenerateTitleAsync(
        string firstUserMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt),
                new(ChatRole.User, firstUserMessage),
            };

            var response = await _chatClient.GetResponseAsync(
                messages,
                options: new ChatOptions
                {
                    ModelId = TitleGenerationModelId,
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
                    "Generated thread title: {Title}", title);

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
