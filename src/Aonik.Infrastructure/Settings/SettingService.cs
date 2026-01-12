using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Abstractions.Settings;
using Aonik.Application.Models.Settings;
using Aonik.Application.Settings;
using Aonik.Domain.Settings;
using Aonik.Domain.Settings.Entities;
using Aonik.SharedKernel.Abstractions;
using Aonik.Application.Abstractions.Multitenancy;

namespace Aonik.Infrastructure.Settings;

public class SettingService : ISettingProvider, ISettingManager
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private readonly IAonikDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _cache;
    private readonly ISettingValueProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserProvider _currentUserProvider;

    public SettingService(
        IAonikDbContext dbContext,
        IConfiguration configuration,
        IMemoryCache cache,
        ISettingValueProtector protector,
        ITenantProvider tenantProvider,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _cache = cache;
        _protector = protector;
        _tenantProvider = tenantProvider;
        _currentUserProvider = currentUserProvider;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return GetForScopeAsync(key, SettingScope.Global, cancellationToken: cancellationToken);
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

    public async Task<string?> GetForScopeAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(scope, key, tenantId, userId);
        if (_cache.TryGetValue(cacheKey, out string? cached))
        {
            return cached;
        }

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

        var stored = await query.FirstOrDefaultAsync(cancellationToken);
        var value = stored?.Value;

        if (!string.IsNullOrWhiteSpace(value) && definition?.IsEncrypted == true)
        {
            value = _protector.Unprotect(value);
        }

        _cache.Set(cacheKey, value, CacheTtl);
        return value;
    }

    public async Task<SettingResolution> GetResolvedAsync(
        string key,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
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

            _cache.Remove(GetCacheKey(scope, key, tenantId, userId));
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
        _cache.Remove(GetCacheKey(scope, key, tenantId, userId));
    }

    public async Task<bool> HasStoredValueAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
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
        return _configuration[$"Settings:{key}"] ?? _configuration[configKey];
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
}
