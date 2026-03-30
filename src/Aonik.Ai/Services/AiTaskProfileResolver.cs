using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

/// <summary>
/// Composes <see cref="IAiModelResolver"/> and <see cref="IPromptStore"/> to resolve
/// the complete AI task profile (model + prompts) for a given use-case.
/// </summary>
internal sealed class AiTaskProfileResolver : IAiTaskProfileResolver
{
    private readonly IAiModelResolver _modelResolver;
    private readonly IPromptStore _promptStore;
    private readonly ILogger<AiTaskProfileResolver> _logger;

    public AiTaskProfileResolver(
        IAiModelResolver modelResolver,
        IPromptStore promptStore,
        ILogger<AiTaskProfileResolver> logger)
    {
        _modelResolver = modelResolver;
        _promptStore = promptStore;
        _logger = logger;
    }

    public async Task<AiTaskProfile> ResolveAsync(
        string useCase,
        string? promptName = null,
        string? defaultModelId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPromptName = promptName ?? useCase;

        // Resolve model
        var modelId = await _modelResolver.ResolveModelNameAsync(useCase, cancellationToken)
            ?? defaultModelId;

        // Resolve system prompt
        string? systemPrompt = null;
        try
        {
            systemPrompt = await _promptStore.LoadPromptAsync(
                resolvedPromptName, "v1", "system", cancellationToken);
        }
        catch (FileNotFoundException)
        {
            _logger.LogDebug(
                "No system prompt template found for '{PromptName}', use-case '{UseCase}'.",
                resolvedPromptName, useCase);
        }

        // Resolve user prompt template
        string? userPromptTemplate = null;
        try
        {
            userPromptTemplate = await _promptStore.LoadPromptAsync(
                resolvedPromptName, "v1", "user", cancellationToken);
        }
        catch (FileNotFoundException)
        {
            // User prompt templates are optional
        }

        return new AiTaskProfile(modelId, systemPrompt, userPromptTemplate);
    }
}
