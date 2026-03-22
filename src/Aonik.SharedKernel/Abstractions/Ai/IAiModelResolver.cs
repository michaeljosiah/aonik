namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Cross-module contract for resolving an AI model name from a use-case key or model ID.
/// Implemented by the AI module; consumed by the Agents module (and others)
/// to determine which LLM model to use for a given task.
///
/// Resolution chain (implemented by AI module):
///   1. <c>AiRoutePolicy</c> tenant-specific override
///   2. <c>AiRoutePolicy</c> global (TenantId = null) default
///   3. Returns <c>null</c> if no policy matches (caller falls back to its own default)
/// </summary>
public interface IAiModelResolver
{
    /// <summary>
    /// Resolves the model name (e.g. "gpt-5-mini") to use for the given use-case.
    /// </summary>
    /// <param name="useCase">Use-case key (e.g. "orchestrator", "title-generation", "finance-agent").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved model name, or <c>null</c> if no mapping exists.</returns>
    Task<string?> ResolveModelNameAsync(string useCase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the model name for a given AI model ID.
    /// </summary>
    /// <param name="modelId">The ID of the AI model.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The model name (e.g. "gpt-5-mini"), or <c>null</c> if the model is not found or inactive.</returns>
    Task<string?> ResolveModelNameByIdAsync(Guid modelId, CancellationToken cancellationToken = default);
}
