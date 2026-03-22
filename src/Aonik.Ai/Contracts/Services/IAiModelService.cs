using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

/// <summary>
/// Service for managing AI providers and models. Provides CRUD operations
/// for the provider/model catalog that backs model selection across agents
/// and non-agent AI tasks.
/// </summary>
public interface IAiModelService
{
    // ── Providers ─────────────────────────────────────────────────────

    Task<IReadOnlyList<AiProviderResponse>> ListProvidersAsync(CancellationToken ct = default);
    Task<AiProviderResponse?> GetProviderAsync(Guid providerId, CancellationToken ct = default);
    Task<AiProviderResponse> CreateProviderAsync(CreateAiProviderRequest request, CancellationToken ct = default);
    Task<AiProviderResponse> UpdateProviderAsync(Guid providerId, UpdateAiProviderRequest request, CancellationToken ct = default);
    Task DeleteProviderAsync(Guid providerId, CancellationToken ct = default);

    // ── Models ────────────────────────────────────────────────────────

    Task<IReadOnlyList<AiModelResponse>> ListModelsAsync(Guid? providerId = null, CancellationToken ct = default);
    Task<AiModelResponse?> GetModelAsync(Guid modelId, CancellationToken ct = default);
    Task<AiModelResponse> CreateModelAsync(CreateAiModelRequest request, CancellationToken ct = default);
    Task<AiModelResponse> UpdateModelAsync(Guid modelId, UpdateAiModelRequest request, CancellationToken ct = default);
    Task DeleteModelAsync(Guid modelId, CancellationToken ct = default);

    // ── Resolution ────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the model name (e.g. "gpt-5-mini") to use for a given use-case.
    /// Resolution chain: AiRoutePolicy (tenant-specific -> global) -> fallback default.
    /// Returns null if no model can be resolved.
    /// </summary>
    Task<string?> ResolveModelNameAsync(string useCase, CancellationToken ct = default);
}
