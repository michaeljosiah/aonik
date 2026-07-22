using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Aonik.Application.Abstractions.Persistence;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Infrastructure.Caching;
using Aonik.SharedKernel.Caching;

namespace Aonik.Infrastructure.Settings;

public class SettingService : ISettingProvider, ISettingManager, ITenantSettingStore
{
    private const string CacheSet = "settings";
    private readonly IAonikDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ICacheStore _cache;
    private readonly ISettingValueProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly IPermissionService _permissionService;
    private readonly ICacheInvalidationPublisher _cacheInvalidationPublisher;

    public SettingService(
        IAonikDbContext dbContext,
        IConfiguration configuration,
        ICacheStore cache,
        ISettingValueProtector protector,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider,
        IPermissionService permissionService,
        ICacheInvalidationPublisher cacheInvalidationPublisher)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _cache = cache;
        _protector = protector;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
        _permissionService = permissionService;
        _cacheInvalidationPublisher = cacheInvalidationPublisher;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return GetGlobalWithFallbackAsync(key, cancellationToken);
    }

    public async Task<string> GetRequiredAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Setting '{key}' is required.");
        }

        return value;
    }

    private async Task<string?> GetGlobalWithFallbackAsync(string key, CancellationToken cancellationToken)
    {
        if (IsConfigurationManagedKey(key))
        {
            var configManagedValue = GetFromConfiguration(key);
            if (!string.IsNullOrWhiteSpace(configManagedValue))
            {
                return configManagedValue;
            }

            return SettingDefinitions.Get(key)?.DefaultValue;
        }

        var globalValue = await GetForScopeAsync(key, SettingScope.Global, cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(globalValue))
        {
            return globalValue;
        }

        var configValue = GetFromConfiguration(key);
        if (!string.IsNullOrWhiteSpace(configValue))
        {
            return configValue;
        }

        return SettingDefinitions.Get(key)?.DefaultValue;
    }

    public async Task<string?> GetForScopeAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (IsConfigurationManagedKey(key))
        {
            if (scope != SettingScope.Global)
            {
                return null;
            }

            var configManagedValue = GetFromConfiguration(key);
            if (!string.IsNullOrWhiteSpace(configManagedValue))
            {
                return configManagedValue;
            }

            return SettingDefinitions.Get(key)?.DefaultValue;
        }

        await EnsureSettingsReadPermissionAsync(scope, cancellationToken);
        return await ReadCoreAsync(key, scope, tenantId, userId, cancellationToken);
    }

    /// <summary>The shared read core: cache, scope query, decryption. Authorization is the
    /// CALLER's concern — <see cref="GetForScopeAsync"/> enforces the platform permission,
    /// <see cref="GetTenantValueAsync"/> relies on the calling module endpoint's policy.</summary>
    private async Task<string?> ReadCoreAsync(
        string key,
        SettingScope scope,
        Guid? tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var cacheKey = GetCacheKey(scope, key, tenantId, userId);

        return await _cache.GetOrSetAsync(
            cacheKey,
            CachePolicy.Short,
            async ct =>
            {
                var definition = SettingDefinitions.Get(key);
                var query = _dbContext.Settings.AsNoTracking().Where(setting => setting.Key == key && setting.Scope == scope);

                if (scope == SettingScope.Tenant)
                {
                    query = query.Where(setting => setting.TenantId == tenantId);
                }
                else if (scope == SettingScope.User)
                {
                    query = query.Where(setting => setting.TenantId == tenantId && setting.UserId == userId);
                }
                else
                {
                    query = query.Where(setting => setting.TenantId == null && setting.UserId == null);
                }

                var stored = await query.FirstOrDefaultAsync(ct);
                var value = stored?.Value;

                if (!string.IsNullOrWhiteSpace(value) && definition?.IsEncrypted == true)
                {
                    value = _protector.Unprotect(value);
                }

                return value;
            },
            CacheSet,
            cancellationToken);
    }

    public async Task<SettingResolution> GetResolvedAsync(
        string key,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (IsConfigurationManagedKey(key))
        {
            var configManagedValue = GetFromConfiguration(key);
            if (configManagedValue != null)
            {
                return new SettingResolution(key, configManagedValue, "Configuration");
            }

            var configManagedDefault = SettingDefinitions.Get(key)?.DefaultValue;
            return new SettingResolution(key, configManagedDefault, configManagedDefault == null ? "None" : "Default");
        }

        await EnsureSettingsReadPermissionAsync(SettingScope.User, cancellationToken);
        tenantId ??= _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId) ? resolvedTenantId : null;
        userId ??= _currentUserProvider.TryGetCurrentUserId(out var resolvedUserId) ? resolvedUserId : null;

        if (userId.HasValue)
        {
            var userValue = await GetForScopeAsync(key, SettingScope.User, tenantId, userId, cancellationToken);
            if (userValue != null)
            {
                return new SettingResolution(key, userValue, "User");
            }
        }

        if (tenantId.HasValue)
        {
            var tenantValue = await GetForScopeAsync(key, SettingScope.Tenant, tenantId, null, cancellationToken);
            if (tenantValue != null)
            {
                return new SettingResolution(key, tenantValue, "Tenant");
            }
        }

        var globalValue = await GetForScopeAsync(key, SettingScope.Global, null, null, cancellationToken);
        if (globalValue != null)
        {
            return new SettingResolution(key, globalValue, "Global");
        }

        var configValue = GetFromConfiguration(key);
        if (configValue != null)
        {
            return new SettingResolution(key, configValue, "Configuration");
        }

        var defaultValue = SettingDefinitions.Get(key)?.DefaultValue;
        return new SettingResolution(key, defaultValue, defaultValue == null ? "None" : "Default");
    }

    public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        return SetAsync(key, value, SettingScope.Global, cancellationToken: cancellationToken);
    }

    public async Task SetAsync(
        string key,
        string? value,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureSettingsWritePermissionAsync(scope, cancellationToken);
        await WriteCoreAsync(key, value, scope, tenantId, userId, cancellationToken);
    }

    /// <summary>The shared write core: config-managed refusal, scope validation, normalization,
    /// encryption, upsert/remove, and cache invalidation. Authorization is the CALLER's concern —
    /// <see cref="SetAsync(string, string?, SettingScope, Guid?, Guid?, CancellationToken)"/>
    /// enforces the platform permission, <see cref="SetTenantValueAsync"/> relies on the calling
    /// module endpoint's policy.</summary>
    private async Task WriteCoreAsync(
        string key,
        string? value,
        SettingScope scope,
        Guid? tenantId,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        if (IsConfigurationManagedKey(key))
        {
            throw new InvalidOperationException(
                $"Setting '{key}' is managed through application configuration and cannot be changed via settings APIs.");
        }

        ValidateScope(scope, tenantId, userId);

        var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var definition = SettingDefinitions.Get(key);

        var existing = await _dbContext.Settings
            .FirstOrDefaultAsync(
                setting => setting.Key == key
                           && setting.Scope == scope
                           && setting.TenantId == tenantId
                           && setting.UserId == userId,
                cancellationToken);

        if (normalized == null)
        {
            if (existing != null)
            {
                _dbContext.Settings.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            await _cacheInvalidationPublisher.PublishAsync(
                new CacheInvalidationEvent(CacheSet, GetCacheKey(scope, key, tenantId, userId)),
                cancellationToken);
            return;
        }

        var storedValue = definition?.IsEncrypted == true
            ? _protector.Protect(normalized)
            : normalized;

        if (existing == null)
        {
            existing = new Setting
            {
                Key = key,
                Value = storedValue,
                Scope = scope,
                TenantId = tenantId,
                UserId = userId
            };

            _dbContext.Settings.Add(existing);
        }
        else
        {
            existing.Value = storedValue;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _cacheInvalidationPublisher.PublishAsync(
            new CacheInvalidationEvent(CacheSet, GetCacheKey(scope, key, tenantId, userId)),
            cancellationToken);
    }

    /// <summary>
    /// <see cref="ITenantSettingStore"/> — the module-owned path (Spec 070 §9). Deliberately does
    /// NOT check the platform <c>Settings.Read</c>/<c>Settings.Write</c> permissions: the calling
    /// module endpoint gates access with its own policy (e.g. Commerce's AdminWritePolicy lets an
    /// Operations user edit storefront settings exactly as they edit products, while the platform
    /// settings surface stays PlatformAdmin/TenantAdmin). Configuration-managed keys stay refused
    /// on write, and encryption/caching behave identically to the managed path.
    /// </summary>
    public Task<string?> GetTenantValueAsync(string key, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required for tenant-scoped settings.");
        }

        return ReadCoreAsync(key, SettingScope.Tenant, tenantId, userId: null, cancellationToken);
    }

    /// <inheritdoc cref="GetTenantValueAsync"/>
    public Task SetTenantValueAsync(string key, string? value, Guid tenantId, CancellationToken cancellationToken = default)
    {
        if (tenantId == Guid.Empty)
        {
            throw new InvalidOperationException("TenantId is required for tenant-scoped settings.");
        }

        return WriteCoreAsync(key, value, SettingScope.Tenant, tenantId, userId: null, cancellationToken);
    }

    public async Task<bool> HasStoredValueAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        if (IsConfigurationManagedKey(key))
        {
            return false;
        }

        ValidateScope(scope, tenantId, userId);

        return await _dbContext.Settings
            .AsNoTracking()
            .AnyAsync(
                setting => setting.Key == key
                           && setting.Scope == scope
                           && setting.TenantId == tenantId
                           && setting.UserId == userId,
                cancellationToken);
    }

    private string? GetFromConfiguration(string key)
    {
        var configKey = key.Replace('.', ':');

        var value = _configuration[$"Settings:{key}"];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = _configuration[configKey];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }

    private static bool IsConfigurationManagedKey(string key)
    {
        return key.StartsWith("Auth.", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetCacheKey(SettingScope scope, string key, Guid? tenantId, Guid? userId)
    {
        return $"settings:{scope}:{tenantId}:{userId}:{key}";
    }

    private static void ValidateScope(SettingScope scope, Guid? tenantId, Guid? userId)
    {
        if (scope == SettingScope.Tenant && (!tenantId.HasValue || tenantId == Guid.Empty))
        {
            throw new InvalidOperationException("TenantId is required for tenant-scoped settings.");
        }

        if (scope == SettingScope.User && (!userId.HasValue || userId == Guid.Empty))
        {
            throw new InvalidOperationException("UserId is required for user-scoped settings.");
        }
    }

    private async Task EnsureSettingsReadPermissionAsync(SettingScope scope, CancellationToken cancellationToken)
    {
        if (scope == SettingScope.Global)
        {
            return;
        }

        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, "Settings.Read", cancellationToken);
        if (!hasPermission)
        {
            throw new PermissionDeniedException("Settings.Read");
        }
    }

    private async Task EnsureSettingsWritePermissionAsync(SettingScope scope, CancellationToken cancellationToken)
    {
        if (scope == SettingScope.Global)
        {
            return;
        }

        var userId = _currentUserProvider.GetCurrentUserId();
        if (!userId.HasValue)
        {
            return;
        }

        var hasPermission = await _permissionService.HasPermissionAsync(userId.Value, "Settings.Write", cancellationToken);
        if (!hasPermission)
        {
            throw new PermissionDeniedException("Settings.Write");
        }
    }
}
