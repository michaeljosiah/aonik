using Aonik.Ai.Contracts.Models;
using Aonik.Ai.Contracts.Services;
using Aonik.Ai.Entities;
using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Ai.Services;

internal sealed class AiModelService : IAiModelService, IAiModelResolver
{
    // AiModels are reference data — once seeded they rarely change. The
    // dev trace audit showed 16 reads per voice run (one per AiRun + a
    // couple of preflight reads). Cache for 30 min with fail-safe; a
    // model edit invalidates the entry from the write paths below.
    private static readonly FusionCacheEntryOptions ModelNameCacheOptions = new(TimeSpan.FromMinutes(30))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(2),
    };

    private const string ModelNameCacheKeyPrefix = "ai-model-name:v1:";
    private static string ModelNameCacheKey(Guid modelId) => $"{ModelNameCacheKeyPrefix}{modelId:N}";

    private readonly AiDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFusionCache _cache;
    private readonly ILogger<AiModelService> _logger;

    public AiModelService(
        AiDbContext dbContext,
        ITenantProvider tenantProvider,
        IFusionCache cache,
        ILogger<AiModelService> logger)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _cache = cache;
        _logger = logger;
    }

    // ── Providers ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AiProviderResponse>> ListProvidersAsync(CancellationToken ct = default)
    {
        var providers = await _dbContext.AiProviders
            .Include(p => p.Models)
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        return providers.Select(MapProviderToResponse).ToList();
    }

    public async Task<AiProviderResponse?> GetProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        var provider = await _dbContext.AiProviders
            .Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == providerId && !p.IsDeleted, ct);

        return provider is null ? null : MapProviderToResponse(provider);
    }

    public async Task<AiProviderResponse> CreateProviderAsync(CreateAiProviderRequest request, CancellationToken ct = default)
    {
        var provider = new AiProvider
        {
            Name = request.Name.Trim(),
            AuthConfigRef = request.AuthConfigRef?.Trim(),
            CapabilitiesJson = request.CapabilitiesJson,
            IsActive = request.IsActive,
        };

        _dbContext.AiProviders.Add(provider);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Created AI provider {ProviderId} '{ProviderName}'", provider.Id, provider.Name);

        return MapProviderToResponse(provider);
    }

    public async Task<AiProviderResponse> UpdateProviderAsync(Guid providerId, UpdateAiProviderRequest request, CancellationToken ct = default)
    {
        var provider = await _dbContext.AiProviders
            .Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == providerId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException($"AI provider {providerId} not found.");

        if (request.Name is not null)
            provider.Name = request.Name.Trim();
        if (request.AuthConfigRef is not null)
            provider.AuthConfigRef = request.AuthConfigRef.Trim();
        if (request.CapabilitiesJson is not null)
            provider.CapabilitiesJson = request.CapabilitiesJson;
        if (request.IsActive.HasValue)
            provider.IsActive = request.IsActive.Value;

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Updated AI provider {ProviderId} '{ProviderName}'", provider.Id, provider.Name);

        return MapProviderToResponse(provider);
    }

    public async Task DeleteProviderAsync(Guid providerId, CancellationToken ct = default)
    {
        var provider = await _dbContext.AiProviders
            .FirstOrDefaultAsync(p => p.Id == providerId && !p.IsDeleted, ct)
            ?? throw new InvalidOperationException($"AI provider {providerId} not found.");

        _dbContext.AiProviders.Remove(provider); // soft-delete via AonikDbContextBase
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted AI provider {ProviderId} '{ProviderName}'", providerId, provider.Name);
    }

    // ── Models ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AiModelResponse>> ListModelsAsync(Guid? providerId = null, CancellationToken ct = default)
    {
        var query = _dbContext.AiModels
            .Where(m => !m.IsDeleted);

        if (providerId.HasValue)
            query = query.Where(m => m.AiProviderId == providerId.Value);

        var models = await query
            .OrderBy(m => m.ModelName)
            .ToListAsync(ct);

        // Batch-load provider names for display
        var providerIds = models.Select(m => m.AiProviderId).Distinct().ToList();
        var providerNames = await _dbContext.AiProviders
            .Where(p => providerIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return models.Select(m => MapModelToResponse(m, providerNames.GetValueOrDefault(m.AiProviderId))).ToList();
    }

    public async Task<AiModelResponse?> GetModelAsync(Guid modelId, CancellationToken ct = default)
    {
        var model = await _dbContext.AiModels
            .FirstOrDefaultAsync(m => m.Id == modelId && !m.IsDeleted, ct);

        if (model is null)
            return null;

        var providerName = await _dbContext.AiProviders
            .Where(p => p.Id == model.AiProviderId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);

        return MapModelToResponse(model, providerName);
    }

    public async Task<AiModelResponse> CreateModelAsync(CreateAiModelRequest request, CancellationToken ct = default)
    {
        // Validate provider exists
        var providerExists = await _dbContext.AiProviders
            .AnyAsync(p => p.Id == request.AiProviderId && !p.IsDeleted, ct);
        if (!providerExists)
            throw new InvalidOperationException($"AI provider {request.AiProviderId} not found.");

        var model = new AiModel
        {
            AiProviderId = request.AiProviderId,
            ModelName = request.ModelName.Trim(),
            ContextWindow = request.ContextWindow,
            CostProfileJson = request.CostProfileJson,
            LatencyProfileJson = request.LatencyProfileJson,
            PolicyTagsJson = request.PolicyTagsJson,
            IsActive = request.IsActive,
        };

        _dbContext.AiModels.Add(model);
        await _dbContext.SaveChangesAsync(ct);

        var providerName = await _dbContext.AiProviders
            .Where(p => p.Id == request.AiProviderId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);

        _logger.LogInformation("Created AI model {ModelId} '{ModelName}' for provider {ProviderId}",
            model.Id, model.ModelName, model.AiProviderId);

        return MapModelToResponse(model, providerName);
    }

    public async Task<AiModelResponse> UpdateModelAsync(Guid modelId, UpdateAiModelRequest request, CancellationToken ct = default)
    {
        var model = await _dbContext.AiModels
            .FirstOrDefaultAsync(m => m.Id == modelId && !m.IsDeleted, ct)
            ?? throw new InvalidOperationException($"AI model {modelId} not found.");

        if (request.ModelName is not null)
            model.ModelName = request.ModelName.Trim();
        if (request.ContextWindow.HasValue)
            model.ContextWindow = request.ContextWindow.Value;
        if (request.CostProfileJson is not null)
            model.CostProfileJson = request.CostProfileJson;
        if (request.LatencyProfileJson is not null)
            model.LatencyProfileJson = request.LatencyProfileJson;
        if (request.PolicyTagsJson is not null)
            model.PolicyTagsJson = request.PolicyTagsJson;
        if (request.IsActive.HasValue)
            model.IsActive = request.IsActive.Value;

        await _dbContext.SaveChangesAsync(ct);

        // Invalidate the cached name so the next reader picks up renames /
        // activation flips on the next request.
        await _cache.RemoveAsync(ModelNameCacheKey(modelId), token: ct);

        var providerName = await _dbContext.AiProviders
            .Where(p => p.Id == model.AiProviderId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct);

        _logger.LogInformation("Updated AI model {ModelId} '{ModelName}'", model.Id, model.ModelName);

        return MapModelToResponse(model, providerName);
    }

    public async Task DeleteModelAsync(Guid modelId, CancellationToken ct = default)
    {
        var model = await _dbContext.AiModels
            .FirstOrDefaultAsync(m => m.Id == modelId && !m.IsDeleted, ct)
            ?? throw new InvalidOperationException($"AI model {modelId} not found.");

        _dbContext.AiModels.Remove(model); // soft-delete via AonikDbContextBase
        await _dbContext.SaveChangesAsync(ct);

        // Invalidate the cached name; the entry is gone.
        await _cache.RemoveAsync(ModelNameCacheKey(modelId), token: ct);

        _logger.LogInformation("Deleted AI model {ModelId} '{ModelName}'", modelId, model.ModelName);
    }

    // ── Resolution ────────────────────────────────────────────────────

    public async Task<string?> ResolveModelNameAsync(string useCase, CancellationToken ct = default)
    {
        var hasTenantContext = _tenantProvider.TryGetCurrentTenantId(out var tenantId);

        var query = _dbContext.AiRoutePolicies
            .AsNoTracking()
            .Where(p => p.UseCase == useCase && p.IsActive && !p.IsDeleted)
            .Where(p => hasTenantContext
                ? p.TenantId == tenantId || p.TenantId == null
                : p.TenantId == null);

        var policy = await query
            .OrderByDescending(p => p.TenantId.HasValue)
            .FirstOrDefaultAsync(ct);

        if (policy is null)
        {
            _logger.LogDebug("No AiRoutePolicy found for use-case '{UseCase}'", useCase);
            return null;
        }

        if (policy.PrimaryModelId == Guid.Empty)
        {
            _logger.LogDebug("AiRoutePolicy for '{UseCase}' has empty PrimaryModelId", useCase);
            return null;
        }

        return await ResolveModelNameByIdAsync(policy.PrimaryModelId, ct);
    }

    public async Task<string?> ResolveModelNameByIdAsync(Guid modelId, CancellationToken ct = default)
    {
        return await _cache.GetOrSetAsync<string?>(
            ModelNameCacheKey(modelId),
            async cacheCt => await LoadModelNameByIdAsync(modelId, cacheCt),
            ModelNameCacheOptions,
            ct);
    }

    private async Task<string?> LoadModelNameByIdAsync(Guid modelId, CancellationToken ct)
    {
        var modelName = await _dbContext.AiModels
            .Where(m => m.Id == modelId && m.IsActive && !m.IsDeleted)
            .Select(m => m.ModelName)
            .FirstOrDefaultAsync(ct);

        if (modelName is not null)
        {
            _logger.LogDebug("Resolved model '{ModelName}' for model ID {ModelId}", modelName, modelId);
        }
        else
        {
            _logger.LogWarning("Model {ModelId} not found or inactive", modelId);
        }

        return modelName;
    }

    // ── Mapping ───────────────────────────────────────────────────────

    private static AiProviderResponse MapProviderToResponse(AiProvider provider)
    {
        return new AiProviderResponse
        {
            Id = provider.Id,
            Name = provider.Name,
            AuthConfigRef = provider.AuthConfigRef,
            CapabilitiesJson = provider.CapabilitiesJson,
            IsActive = provider.IsActive,
            Models = provider.Models
                .Where(m => !m.IsDeleted)
                .Select(m => MapModelToResponse(m, provider.Name))
                .ToList(),
            CreatedAt = provider.CreatedAt,
            UpdatedAt = provider.UpdatedAt,
        };
    }

    private static AiModelResponse MapModelToResponse(AiModel model, string? providerName)
    {
        return new AiModelResponse
        {
            Id = model.Id,
            AiProviderId = model.AiProviderId,
            ProviderName = providerName,
            ModelName = model.ModelName,
            ContextWindow = model.ContextWindow,
            CostProfileJson = model.CostProfileJson,
            LatencyProfileJson = model.LatencyProfileJson,
            PolicyTagsJson = model.PolicyTagsJson,
            IsActive = model.IsActive,
            CreatedAt = model.CreatedAt,
            UpdatedAt = model.UpdatedAt,
        };
    }
}
