namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Resolves the complete AI task profile (model + prompts) for a given use-case.
/// Composes <see cref="IAiModelResolver"/> with prompt templates read from the
/// AiTask table into a single call, applying tenant-level overrides where configured.
/// </summary>
public interface IAiTaskProfileResolver
{
    /// <summary>
    /// Resolves the model name and prompt templates for the given use-case.
    /// </summary>
    /// <param name="useCase">Use-case key for model resolution (e.g. "title-generation").</param>
    /// <param name="promptName">Prompt template name. Defaults to <paramref name="useCase"/> if null.</param>
    /// <param name="defaultModelId">Fallback model name if no <c>AiRoutePolicy</c> matches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<AiTaskProfile> ResolveAsync(
        string useCase,
        string? promptName = null,
        string? defaultModelId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolved AI task profile containing the model and prompts to use for an LLM call.
/// </summary>
/// <param name="ModelId">The resolved model name (e.g. "gpt-5-mini"), or null if no model could be resolved.</param>
/// <param name="SystemPrompt">The resolved system prompt content, or null if no prompt template exists.</param>
/// <param name="UserPromptTemplate">The resolved user prompt template content, or null if not defined.</param>
public sealed record AiTaskProfile(
    string? ModelId,
    string? SystemPrompt,
    string? UserPromptTemplate);
