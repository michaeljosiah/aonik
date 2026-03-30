using Aonik.Ai.Persistence;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Caching;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Ai.Services;

/// <summary>
/// Tenant-aware prompt store that checks the database for tenant-specific or global
/// <c>PromptSpec</c> overrides before falling back to file-based templates.
///
/// Resolution chain:
///   1. DB PromptSpec with matching TenantId (tenant-specific override)
///   2. DB PromptSpec with TenantId = null (global override)
///   3. File-based template via <see cref="FileBasedPromptStore"/>
/// </summary>
internal sealed class TenantAwarePromptStore : IPromptStore
{
    private readonly AiDbContext _dbContext;
    private readonly FileBasedPromptStore _fileStore;
    private readonly ICacheStore _cacheStore;
    private readonly ITenantProvider _tenantProvider;

    private const string CacheSet = "prompt-specs";

    public TenantAwarePromptStore(
        AiDbContext dbContext,
        FileBasedPromptStore fileStore,
        ICacheStore cacheStore,
        ITenantProvider tenantProvider)
    {
        _dbContext = dbContext;
        _fileStore = fileStore;
        _cacheStore = cacheStore;
        _tenantProvider = tenantProvider;
    }

    public async Task<string> LoadPromptAsync(
        string promptName,
        string version = "v1",
        string role = "system",
        CancellationToken cancellationToken = default)
    {
        _tenantProvider.TryGetCurrentTenantId(out var tenantId);
        var cacheKey = $"prompt:{tenantId}:{promptName}:{version}:{role}";

        var cached = await _cacheStore.GetOrSetAsync<string>(
            cacheKey,
            CachePolicy.Medium,
            async ct =>
            {
                var dbResult = await ResolveFromDatabaseAsync(promptName, version, role, ct);
                return dbResult;
            },
            CacheSet,
            cancellationToken);

        if (!string.IsNullOrEmpty(cached))
            return cached;

        // Fall back to file-based template
        return await _fileStore.LoadPromptAsync(promptName, version, role, cancellationToken);
    }

    private async Task<string?> ResolveFromDatabaseAsync(
        string promptName,
        string version,
        string role,
        CancellationToken cancellationToken)
    {
        var hasTenantContext = _tenantProvider.TryGetCurrentTenantId(out var tenantId);

        var query = _dbContext.PromptSpecs
            .AsNoTracking()
            .Where(p => p.Name == promptName
                && p.Version == version
                && p.IsPublished)
            .Where(p => hasTenantContext
                ? p.TenantId == tenantId || p.TenantId == null
                : p.TenantId == null);

        var promptSpec = await query
            .OrderByDescending(p => p.TenantId.HasValue)
            .FirstOrDefaultAsync(cancellationToken);

        if (promptSpec is null)
            return null;

        var template = role.ToLowerInvariant() switch
        {
            "system" => promptSpec.SystemTemplate,
            "user" => promptSpec.UserTemplate,
            "developer" => promptSpec.DeveloperTemplate,
            _ => null
        };

        return string.IsNullOrEmpty(template) ? null : template;
    }
}
