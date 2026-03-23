using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Ai.ModelCatalog;

internal sealed class ModelsDevAiModelCatalogSource : IAiModelCatalogSource
{
    private const string CacheKey = "ai-model-catalog:models-dev";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ModelsDevAiModelCatalogSource> _logger;

    public ModelsDevAiModelCatalogSource(
        HttpClient httpClient,
        IMemoryCache memoryCache,
        ILogger<ModelsDevAiModelCatalogSource> logger)
    {
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiCatalogModelProviderResponse>> ListModelProvidersAsync(CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(ct);
        return catalog.ModelProviders;
    }

    public async Task<AiCatalogModelProviderResponse?> GetModelProviderAsync(string modelProviderKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelProviderKey))
            return null;

        var catalog = await GetCatalogAsync(ct);
        catalog.ModelProvidersByKey.TryGetValue(modelProviderKey.Trim(), out var modelProvider);
        return modelProvider;
    }

    public async Task<IReadOnlyList<AiCatalogModelResponse>> ListModelsAsync(string modelProviderKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modelProviderKey))
            return [];

        var catalog = await GetCatalogAsync(ct);
        return catalog.ModelsByProviderKey.TryGetValue(modelProviderKey.Trim(), out var models)
            ? models
            : [];
    }

    private async Task<CatalogCacheItem> GetCatalogAsync(CancellationToken ct)
    {
        var cachedCatalog = await _memoryCache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;

            _logger.LogInformation("Fetching AI model catalog from configured external source.");

            var payload = await _httpClient.GetFromJsonAsync<Dictionary<string, ModelProviderPayload>>("/api.json", JsonOptions, ct)
                ?? throw new InvalidOperationException("Configured AI model catalog source returned no payload.");

            return MapCatalog(payload);
        });

        return cachedCatalog ?? throw new InvalidOperationException("Failed to cache AI model catalog data.");
    }

    private static CatalogCacheItem MapCatalog(IReadOnlyDictionary<string, ModelProviderPayload> payload)
    {
        var modelProvidersByKey = new Dictionary<string, AiCatalogModelProviderResponse>(StringComparer.OrdinalIgnoreCase);
        var modelsByProviderKey = new Dictionary<string, IReadOnlyList<AiCatalogModelResponse>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (providerKey, providerPayload) in payload)
        {
            var normalizedProviderKey = string.IsNullOrWhiteSpace(providerPayload.Id) ? providerKey : providerPayload.Id.Trim();

            var models = providerPayload.Models
                .Select(model => MapModel(normalizedProviderKey, model.Key, model.Value))
                .OrderBy(model => model.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var modelProvider = new AiCatalogModelProviderResponse
            {
                ModelProviderKey = normalizedProviderKey,
                Name = string.IsNullOrWhiteSpace(providerPayload.Name) ? normalizedProviderKey : providerPayload.Name.Trim(),
                DocumentationUrl = providerPayload.Doc,
                SdkPackage = providerPayload.Npm,
                ApiBaseUrl = providerPayload.Api,
                EnvironmentVariables = providerPayload.Env
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                ModelCount = models.Count,
            };

            modelProvidersByKey[normalizedProviderKey] = modelProvider;
            modelsByProviderKey[normalizedProviderKey] = models;
        }

        return new CatalogCacheItem
        {
            ModelProviders = modelProvidersByKey.Values
                .OrderBy(modelProvider => modelProvider.Name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            ModelProvidersByKey = modelProvidersByKey,
            ModelsByProviderKey = modelsByProviderKey,
        };
    }

    private static AiCatalogModelResponse MapModel(string modelProviderKey, string modelKey, ModelPayload modelPayload)
    {
        var normalizedModelKey = string.IsNullOrWhiteSpace(modelPayload.Id) ? modelKey : modelPayload.Id.Trim();

        return new AiCatalogModelResponse
        {
            ModelProviderKey = modelProviderKey,
            ModelKey = normalizedModelKey,
            Name = string.IsNullOrWhiteSpace(modelPayload.Name) ? normalizedModelKey : modelPayload.Name.Trim(),
            Family = string.IsNullOrWhiteSpace(modelPayload.Family) ? null : modelPayload.Family.Trim(),
            ContextWindow = modelPayload.Limit?.Context ?? 0,
            OutputTokenLimit = modelPayload.Limit?.Output ?? 0,
            CostProfileJson = modelPayload.Cost is JsonElement costElement && costElement.ValueKind != JsonValueKind.Undefined
                ? costElement.GetRawText()
                : "{}",
            InputModalities = modelPayload.Modalities?.Input
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],
            OutputModalities = modelPayload.Modalities?.Output
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [],
            SupportsReasoning = modelPayload.Reasoning,
            SupportsToolCall = modelPayload.ToolCall,
            SupportsStructuredOutput = modelPayload.StructuredOutput,
            SupportsAttachments = modelPayload.Attachment,
            IsOpenWeights = modelPayload.OpenWeights,
        };
    }

    private sealed record CatalogCacheItem
    {
        public required IReadOnlyList<AiCatalogModelProviderResponse> ModelProviders { get; init; }
        public required IReadOnlyDictionary<string, AiCatalogModelProviderResponse> ModelProvidersByKey { get; init; }
        public required IReadOnlyDictionary<string, IReadOnlyList<AiCatalogModelResponse>> ModelsByProviderKey { get; init; }
    }

    private sealed record ModelProviderPayload
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Doc { get; init; }
        public string? Npm { get; init; }
        public string? Api { get; init; }
        public IReadOnlyList<string> Env { get; init; } = [];
        public IReadOnlyDictionary<string, ModelPayload> Models { get; init; } = new Dictionary<string, ModelPayload>();
    }

    private sealed record ModelPayload
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? Family { get; init; }
        public JsonElement? Cost { get; init; }
        public ModelLimitPayload? Limit { get; init; }
        public ModelModalitiesPayload? Modalities { get; init; }
        public bool Attachment { get; init; }
        public bool Reasoning { get; init; }

        [JsonPropertyName("tool_call")]
        public bool ToolCall { get; init; }

        [JsonPropertyName("structured_output")]
        public bool StructuredOutput { get; init; }

        [JsonPropertyName("open_weights")]
        public bool OpenWeights { get; init; }
    }

    private sealed record ModelLimitPayload
    {
        public int Context { get; init; }
        public int Output { get; init; }
    }

    private sealed record ModelModalitiesPayload
    {
        public IReadOnlyList<string> Input { get; init; } = [];
        public IReadOnlyList<string> Output { get; init; } = [];
    }
}
