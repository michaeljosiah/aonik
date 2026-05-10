using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Platform.Services.Settings;

/// <summary>
/// Voice provider credential storage. Encrypts API keys at rest with ASP.NET
/// Core Data Protection, exposes status-only readback (never echoes the raw
/// secret), and resolves credentials in priority order:
/// tenant override → host default → configuration fallback. Mirrors
/// <see cref="TextToSpeechCredentialSettingsService"/>.
/// </summary>
internal sealed class VoiceProviderCredentialSettingsService : IVoiceProviderCredentialSettingsService
{
    private const string SettingProtectionPurpose = "Aonik.Settings";

    private static readonly FusionCacheEntryOptions CacheEntryOptions = new(TimeSpan.FromMinutes(10))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(1),
    };

    private const string CacheKeyPrefix = "voice-credentials:v1:";

    private readonly PlatformDbContext _dbContext;
    private readonly IDataProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;
    private readonly IFusionCache _cache;

    public VoiceProviderCredentialSettingsService(
        PlatformDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        ITenantProvider tenantProvider,
        IConfiguration configuration,
        IFusionCache cache)
    {
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector(SettingProtectionPurpose);
        _tenantProvider = tenantProvider;
        _configuration = configuration;
        _cache = cache;
    }

    private static string ResolveCacheKey(string provider, Guid? tenantId)
        => tenantId is { } id
            ? $"{CacheKeyPrefix}{id:N}:{provider.ToLowerInvariant()}"
            : $"{CacheKeyPrefix}host:{provider.ToLowerInvariant()}";

    public async Task<VoiceProviderCredentialSnapshot> GetHostAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalized = VoiceProviderSettingNames.NormalizeProvider(provider);
        var key = VoiceProviderSettingNames.GetProviderApiKeySettingName(normalized);

        var hasHost = await HasStoredValueAsync(key, SettingScope.Global, cancellationToken: cancellationToken);
        var hasTenantOverride = _tenantProvider.TryGetCurrentTenantId(out var tenantId)
            && await HasStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken);
        var source = await ResolveSourceAsync(key, normalized, tenantId, cancellationToken);

        return new VoiceProviderCredentialSnapshot(normalized, hasHost, hasTenantOverride, source);
    }

    public async Task<VoiceProviderCredentialSnapshot> SaveHostAsync(
        VoiceProviderCredentialUpdate update,
        CancellationToken cancellationToken = default)
    {
        var normalized = VoiceProviderSettingNames.NormalizeProvider(update.Provider);
        var key = VoiceProviderSettingNames.GetProviderApiKeySettingName(normalized);
        await SaveAsync(key, SettingScope.Global, null, update, cancellationToken);
        await InvalidateAsync(normalized, tenantId: null, cancellationToken);
        return await GetHostAsync(normalized, cancellationToken);
    }

    public async Task<VoiceProviderCredentialSnapshot> GetTenantAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalized = VoiceProviderSettingNames.NormalizeProvider(provider);
        var key = VoiceProviderSettingNames.GetProviderApiKeySettingName(normalized);

        var hasHost = await HasStoredValueAsync(key, SettingScope.Global, cancellationToken: cancellationToken);
        var hasTenantOverride = await HasStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken);
        var source = await ResolveSourceAsync(key, normalized, tenantId, cancellationToken);

        return new VoiceProviderCredentialSnapshot(normalized, hasHost, hasTenantOverride, source);
    }

    public async Task<VoiceProviderCredentialSnapshot> SaveTenantAsync(
        VoiceProviderCredentialUpdate update,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalized = VoiceProviderSettingNames.NormalizeProvider(update.Provider);
        var key = VoiceProviderSettingNames.GetProviderApiKeySettingName(normalized);
        await SaveAsync(key, SettingScope.Tenant, tenantId, update, cancellationToken);
        await InvalidateAsync(normalized, tenantId, cancellationToken);
        return await GetTenantAsync(normalized, cancellationToken);
    }

    public async Task<VoiceProviderCredentialResolution> ResolveAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalized = VoiceProviderSettingNames.NormalizeProvider(provider);
        var tenantId = _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId)
            ? resolvedTenantId
            : (Guid?)null;

        var cacheKey = ResolveCacheKey(normalized, tenantId);
        var cached = await _cache.GetOrSetAsync<VoiceProviderCredentialResolution>(
            cacheKey,
            async ct => await ResolveFromStoreAsync(normalized, tenantId, ct),
            CacheEntryOptions,
            cancellationToken);

        return cached!;
    }

    private async Task<VoiceProviderCredentialResolution> ResolveFromStoreAsync(
        string normalized,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var key = VoiceProviderSettingNames.GetProviderApiKeySettingName(normalized);

        if (tenantId.HasValue)
        {
            var tenantValue = await GetStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(tenantValue))
            {
                return new VoiceProviderCredentialResolution(
                    normalized,
                    tenantValue,
                    "TenantOverride",
                    HasCredential: true,
                    IsTenantOverride: true);
            }
        }

        var hostValue = await GetStoredValueAsync(key, SettingScope.Global, null, cancellationToken);
        if (!string.IsNullOrWhiteSpace(hostValue))
        {
            return new VoiceProviderCredentialResolution(
                normalized,
                hostValue,
                "HostDefault",
                HasCredential: true,
                IsTenantOverride: false);
        }

        var configFallback = GetConfigurationFallback(normalized);
        return new VoiceProviderCredentialResolution(
            normalized,
            configFallback,
            string.IsNullOrWhiteSpace(configFallback) ? "Missing" : "Configuration",
            HasCredential: !string.IsNullOrWhiteSpace(configFallback),
            IsTenantOverride: false);
    }

    private async Task SaveAsync(
        string key,
        SettingScope scope,
        Guid? tenantId,
        VoiceProviderCredentialUpdate update,
        CancellationToken cancellationToken)
    {
        var normalizedApiKey = string.IsNullOrWhiteSpace(update.ApiKey) ? null : update.ApiKey.Trim();

        var existing = await _dbContext.Settings.FirstOrDefaultAsync(
            item => item.Key == key
                    && item.Scope == scope
                    && item.TenantId == tenantId
                    && item.UserId == null,
            cancellationToken);

        if (update.ClearStoredValue)
        {
            if (existing != null)
            {
                _dbContext.Settings.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(normalizedApiKey)) return;

        var protectedValue = _protector.Protect(normalizedApiKey);
        if (existing == null)
        {
            existing = new Setting
            {
                Key = key,
                Scope = scope,
                TenantId = tenantId,
                UserId = null,
                Value = protectedValue,
            };
            _dbContext.Settings.Add(existing);
        }
        else
        {
            existing.Value = protectedValue;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> HasStoredValueAsync(
        string key,
        SettingScope scope,
        Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Settings
            .AsNoTracking()
            .AnyAsync(item => item.Key == key
                              && item.Scope == scope
                              && item.TenantId == tenantId
                              && item.UserId == null,
                cancellationToken);
    }

    private async Task<string?> GetStoredValueAsync(
        string key,
        SettingScope scope,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var storedValue = await _dbContext.Settings
            .AsNoTracking()
            .Where(item => item.Key == key
                           && item.Scope == scope
                           && item.TenantId == tenantId
                           && item.UserId == null)
            .Select(item => item.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrWhiteSpace(storedValue) ? null : _protector.Unprotect(storedValue);
    }

    private async Task<string> ResolveSourceAsync(
        string key,
        string normalizedProvider,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId.HasValue && await HasStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken))
            return "TenantOverride";

        if (await HasStoredValueAsync(key, SettingScope.Global, cancellationToken: cancellationToken))
            return "HostDefault";

        return string.IsNullOrWhiteSpace(GetConfigurationFallback(normalizedProvider))
            ? "Missing"
            : "Configuration";
    }

    private string? GetConfigurationFallback(string provider)
    {
        // Convention: AI:Voice:{Provider}ApiKey  (e.g. OpenAIApiKey, AzureApiKey).
        // Falls back to the existing AI:OpenAI:ApiKey for OpenAI so dev setups
        // that already have the key don't need a duplicate entry.
        return _configuration[$"AI:Voice:{provider}ApiKey"]
               ?? (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
                   ? _configuration["AI:OpenAI:ApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                   : null);
    }

    private async Task InvalidateAsync(string provider, Guid? tenantId, CancellationToken cancellationToken)
    {
        var key = ResolveCacheKey(provider, tenantId);
        await _cache.RemoveAsync(key, token: cancellationToken);
    }
}
