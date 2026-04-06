using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Aonik.Ai.Services;

/// <summary>
/// Resolves the complete AI task profile (model + prompts) for a given use-case.
/// Queries <see cref="Entities.AiTask"/> directly for prompt templates (replacing
/// the previous <c>IPromptStore</c> → <c>TenantAwarePromptStore</c> chain) and
/// delegates model resolution to <see cref="IAiModelResolver"/>.
///
/// Tenant precedence: tenant-specific AiTask row → global AiTask row.
/// </summary>
internal sealed class AiTaskProfileResolver : IAiTaskProfileResolver
{
    private readonly AiDbContext _dbContext;
    private readonly IAiModelResolver _modelResolver;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICacheStore _cacheStore;
    private readonly ILogger<AiTaskProfileResolver> _logger;

    private const string CacheSet = "ai-tasks";

    public AiTaskProfileResolver(
        AiDbContext dbContext,
        IAiModelResolver modelResolver,
        ITenantProvider tenantProvider,
        ICacheStore cacheStore,
        ILogger<AiTaskProfileResolver> logger)
    {
        _dbContext = dbContext;
        _modelResolver = modelResolver;
        _tenantProvider = tenantProvider;
        _cacheStore = cacheStore;
        _logger = logger;
    }

    public async Task<AiTaskProfile> ResolveAsync(
        string useCase,
        string? promptName = null,
        string? defaultModelId = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedPromptName = promptName ?? useCase;

        // Resolve model via route policy (unchanged)
        var modelId = await _modelResolver.ResolveModelNameAsync(useCase, cancellationToken)
            ?? defaultModelId;

        // Resolve prompts from AiTask table
        var (systemPrompt, userPromptTemplate) = await ResolvePromptsAsync(
            resolvedPromptName, cancellationToken);

        return new AiTaskProfile(modelId, systemPrompt, userPromptTemplate);
    }

    private async Task<(string? SystemPrompt, string? UserPromptTemplate)> ResolvePromptsAsync(
        string promptName,
        CancellationToken cancellationToken)
    {
        _tenantProvider.TryGetCurrentTenantId(out var tenantId);
        var cacheKey = $"ai-task-prompt:{tenantId}:{promptName}";

        var cached = await _cacheStore.GetOrSetAsync<CachedPrompts?>(
            cacheKey,
            CachePolicy.Medium,
            async ct => await LoadPromptsFromDbAsync(promptName, ct),
            CacheSet,
            cancellationToken);

        return (cached?.SystemPrompt, cached?.UserPromptTemplate);
    }

    private async Task<CachedPrompts?> LoadPromptsFromDbAsync(
        string promptName,
        CancellationToken cancellationToken)
    {
        var hasTenantContext = _tenantProvider.TryGetCurrentTenantId(out var tenantId);

        var aiTask = await _dbContext.AiTasks
            .AsNoTracking()
            .Where(t => t.PromptName == promptName
                && t.IsPublished)
            .Where(t => hasTenantContext
                ? t.TenantId == tenantId || t.TenantId == null
                : t.TenantId == null)
            .OrderByDescending(t => t.TenantId.HasValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (aiTask is null)
        {
            _logger.LogDebug(
                "No AiTask found for prompt name '{PromptName}'. Returning empty profile.",
                promptName);
            return null;
        }

        var systemPrompt = string.IsNullOrEmpty(aiTask.SystemTemplate) ? null : aiTask.SystemTemplate;
        var userPromptTemplate = string.IsNullOrEmpty(aiTask.UserTemplate) ? null : aiTask.UserTemplate;

        return new CachedPrompts(systemPrompt, userPromptTemplate);
    }

    private sealed record CachedPrompts(string? SystemPrompt, string? UserPromptTemplate);
}
