using System.Text.Json;

using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

internal sealed class AiModelCatalogImportService : IAiModelCatalogImportService
{
    private readonly AiDbContext _dbContext;
    private readonly IAiModelCatalogSource _catalogSource;
    private readonly ILogger<AiModelCatalogImportService> _logger;

    public AiModelCatalogImportService(
        AiDbContext dbContext,
        IAiModelCatalogSource catalogSource,
        ILogger<AiModelCatalogImportService> logger)
    {
        _dbContext = dbContext;
        _catalogSource = catalogSource;
        _logger = logger;
    }

    public async Task<ImportAiCatalogModelProviderResponse> ImportModelProviderAsync(
        string modelProviderKey,
        ImportAiCatalogModelProviderRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelProviderKey))
            throw new InvalidOperationException("Model provider key is required.");

        var normalizedKey = modelProviderKey.Trim();

        var catalogModelProvider = await _catalogSource.GetModelProviderAsync(normalizedKey, ct)
            ?? throw new InvalidOperationException($"Model provider '{normalizedKey}' was not found in the configured catalog source.");

        var catalogModels = await _catalogSource.ListModelsAsync(normalizedKey, ct);

        var (aiProvider, providerCreated) = await ResolveAiProviderAsync(catalogModelProvider, ct);

        if (providerCreated)
        {
            _dbContext.AiProviders.Add(aiProvider);
        }

        var existingModels = await _dbContext.AiModels
            .Where(m => m.AiProviderId == aiProvider.Id && !m.IsDeleted)
            .ToListAsync(ct);

        var createdCount = 0;
        var linkedCount = 0;
        var skippedCount = 0;

        foreach (var catalogModel in catalogModels)
        {
            var existingByExternalKey = existingModels.FirstOrDefault(model =>
                string.Equals(model.ExternalModelKey, catalogModel.ModelKey, StringComparison.OrdinalIgnoreCase));

            if (existingByExternalKey is not null)
            {
                skippedCount++;
                continue;
            }

            var nameMatches = existingModels.Where(model =>
                    string.IsNullOrWhiteSpace(model.ExternalModelKey)
                    && string.Equals(model.ModelName, catalogModel.ModelKey, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nameMatches.Count == 1)
            {
                nameMatches[0].ExternalModelKey = catalogModel.ModelKey;
                linkedCount++;
                continue;
            }

            var newModel = new AiModel
            {
                AiProviderId = aiProvider.Id,
                ExternalModelKey = catalogModel.ModelKey,
                ModelName = catalogModel.ModelKey,
                ContextWindow = catalogModel.ContextWindow,
                CostProfileJson = catalogModel.CostProfileJson,
                LatencyProfileJson = "{}",
                PolicyTagsJson = BuildPolicyTagsJson(catalogModel),
                IsActive = !request.ImportModelsAsInactive,
            };

            _dbContext.AiModels.Add(newModel);
            existingModels.Add(newModel);
            createdCount++;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Imported model provider {ModelProviderKey} into AI provider {AiProviderId}. ProviderCreated={ProviderCreated}, ModelsCreated={ModelsCreated}, ModelsLinked={ModelsLinked}, ModelsSkipped={ModelsSkipped}",
            normalizedKey,
            aiProvider.Id,
            providerCreated,
            createdCount,
            linkedCount,
            skippedCount);

        return new ImportAiCatalogModelProviderResponse
        {
            AiProviderId = aiProvider.Id,
            ModelProviderKey = catalogModelProvider.ModelProviderKey,
            ProviderName = aiProvider.Name,
            ProviderCreated = providerCreated,
            ModelsCreated = createdCount,
            ModelsLinked = linkedCount,
            ModelsSkipped = skippedCount,
        };
    }

    private async Task<(AiProvider Provider, bool ProviderCreated)> ResolveAiProviderAsync(
        AiCatalogModelProviderResponse catalogModelProvider,
        CancellationToken ct)
    {
        var providerByExternalKey = await _dbContext.AiProviders
            .FirstOrDefaultAsync(provider =>
                !provider.IsDeleted
                && provider.ExternalModelProviderKey != null
                && provider.ExternalModelProviderKey == catalogModelProvider.ModelProviderKey,
                ct);

        if (providerByExternalKey is not null)
            return (providerByExternalKey, false);

        var providerCandidates = await _dbContext.AiProviders
            .Where(provider => !provider.IsDeleted)
            .ToListAsync(ct);

        var providerNameMatches = providerCandidates
            .Where(provider => string.IsNullOrWhiteSpace(provider.ExternalModelProviderKey)
                && string.Equals(provider.Name, catalogModelProvider.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (providerNameMatches.Count == 1)
        {
            var existingProvider = providerNameMatches[0];
            existingProvider.ExternalModelProviderKey = catalogModelProvider.ModelProviderKey;
            return (existingProvider, false);
        }

        return (
            new AiProvider
            {
                Name = catalogModelProvider.Name,
                ExternalModelProviderKey = catalogModelProvider.ModelProviderKey,
                AuthConfigRef = null,
                CapabilitiesJson = BuildCapabilitiesJson(catalogModelProvider),
                IsActive = false,
            },
            true);
    }

    private static string BuildCapabilitiesJson(AiCatalogModelProviderResponse catalogModelProvider)
    {
        return JsonSerializer.Serialize(new
        {
            source = "external-model-catalog",
            modelProviderKey = catalogModelProvider.ModelProviderKey,
            documentationUrl = catalogModelProvider.DocumentationUrl,
            sdkPackage = catalogModelProvider.SdkPackage,
            apiBaseUrl = catalogModelProvider.ApiBaseUrl,
            environmentVariables = catalogModelProvider.EnvironmentVariables,
        });
    }

    private static string BuildPolicyTagsJson(AiCatalogModelResponse catalogModel)
    {
        var tags = new List<string>
        {
            "source:external-model-catalog",
            $"model-provider:{catalogModel.ModelProviderKey}",
        };

        if (!string.IsNullOrWhiteSpace(catalogModel.Family))
        {
            tags.Add($"family:{catalogModel.Family}");
        }

        if (catalogModel.SupportsReasoning)
            tags.Add("reasoning");
        if (catalogModel.SupportsToolCall)
            tags.Add("tool-call");
        if (catalogModel.SupportsStructuredOutput)
            tags.Add("structured-output");
        if (catalogModel.SupportsAttachments)
            tags.Add("attachments");
        if (catalogModel.IsOpenWeights)
            tags.Add("open-weights");

        foreach (var inputModality in catalogModel.InputModalities)
        {
            tags.Add($"input:{inputModality}");
        }

        foreach (var outputModality in catalogModel.OutputModalities)
        {
            tags.Add($"output:{outputModality}");
        }

        return JsonSerializer.Serialize(tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }
}
