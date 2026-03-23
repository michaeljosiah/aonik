using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

public interface IAiModelCatalogSource
{
    Task<IReadOnlyList<AiCatalogModelProviderResponse>> ListModelProvidersAsync(CancellationToken ct = default);
    Task<AiCatalogModelProviderResponse?> GetModelProviderAsync(string modelProviderKey, CancellationToken ct = default);
    Task<IReadOnlyList<AiCatalogModelResponse>> ListModelsAsync(string modelProviderKey, CancellationToken ct = default);
}
