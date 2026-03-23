using Aonik.Ai.Contracts.Models;

namespace Aonik.Ai.Contracts.Services;

public interface IAiModelCatalogImportService
{
    Task<ImportAiCatalogModelProviderResponse> ImportModelProviderAsync(
        string modelProviderKey,
        ImportAiCatalogModelProviderRequest request,
        CancellationToken ct = default);
}
