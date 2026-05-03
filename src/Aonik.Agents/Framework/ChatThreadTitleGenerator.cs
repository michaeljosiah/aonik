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
/// The model and prompt are resolved via <see cref="IAiTaskProfileResolver"/>
/// using the "title-generation" use-case key. If no policy is configured, falls
/// back to <see cref="DefaultTitleModelId"/>.
/// </summary>
internal sealed class ChatThreadTitleGenerator : IChatThreadTitleGenerator
{
    private readonly IChatClient _chatClient;
    private readonly IAiTaskProfileResolver _profileResolver;
    private readonly ILogger<ChatThreadTitleGenerator> _logger;
    private const string DefaultTitleModelId = "gpt-5-nano";
    private const string TitleGenerationUseCase = "title-generation";
    private const string PromptName = "thread_title";

    public ChatThreadTitleGenerator(
        IChatClient chatClient,
        IAiTaskProfileResolver profileResolver,
        ILogger<ChatThreadTitleGenerator> logger)
    {
        _chatClient = chatClient;
        _profileResolver = profileResolver;
        _logger = logger;
    }

    public async Task<string> GenerateTitleAsync(
        string firstUserMessage,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var profile = await _profileResolver.ResolveAsync(
                TitleGenerationUseCase, PromptName, DefaultTitleModelId, cancellationToken);

            var messages = new List<ChatMessage>();
            if (!string.IsNullOrEmpty(profile.SystemPrompt))
                messages.Add(new ChatMessage(ChatRole.System, profile.SystemPrompt));
            messages.Add(new ChatMessage(ChatRole.User, firstUserMessage));

            // Stamp the use_case so the AiTraceObservation row carries a
            // semantic trace name ("title-generation") instead of leaking the
            // model id via AuditMiddleware's legacy fallback. Without this,
            // dedupe in the trace explorer can pick this ancillary call as
            // the representative for the parent run and show a confusing
            // model-id-as-trace-name (e.g. "gpt-5-nano").
            var options = new ChatOptions
            {
                ModelId = profile.ModelId ?? DefaultTitleModelId,
            };
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties[AiTelemetry.UseCaseAttribute] = TitleGenerationUseCase;

            var response = await _chatClient.GetResponseAsync(
                messages,
                options: options,
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
                    "Generated thread title: {Title} (model: {ModelId})", title, profile.ModelId);

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
