using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

using Aonik.Platform.Contracts.Models.Settings;
using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;

namespace Aonik.Platform.Services.Settings;

internal sealed class TextToSpeechCredentialSettingsService : ITextToSpeechCredentialSettingsService
{
    private const string SettingProtectionPurpose = "Aonik.Settings";

    private readonly PlatformDbContext _dbContext;
    private readonly IDataProtector _protector;
    private readonly ITenantProvider _tenantProvider;
    private readonly IConfiguration _configuration;

    public TextToSpeechCredentialSettingsService(
        PlatformDbContext dbContext,
        IDataProtectionProvider dataProtectionProvider,
        ITenantProvider tenantProvider,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _protector = dataProtectionProvider.CreateProtector(SettingProtectionPurpose);
        _tenantProvider = tenantProvider;
        _configuration = configuration;
    }

    public async Task<TextToSpeechCredentialSnapshot> GetHostAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var key = GetProviderSettingKey(normalizedProvider);

        var hasHostCredential = await HasStoredValueAsync(key, SettingScope.Global, cancellationToken: cancellationToken);
        var hasTenantOverride = _tenantProvider.TryGetCurrentTenantId(out var tenantId)
            && await HasStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken);
        var effectiveSource = await ResolveSourceAsync(key, tenantId, cancellationToken);

        return new TextToSpeechCredentialSnapshot(normalizedProvider, hasHostCredential, hasTenantOverride, effectiveSource);
    }

    public async Task<TextToSpeechCredentialSnapshot> SaveHostAsync(
        TextToSpeechCredentialUpdate update,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = NormalizeProvider(update.Provider);
        var key = GetProviderSettingKey(normalizedProvider);
        await SaveAsync(key, SettingScope.Global, null, update, cancellationToken);
        return await GetHostAsync(normalizedProvider, cancellationToken);
    }

    public async Task<TextToSpeechCredentialSnapshot> GetTenantAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalizedProvider = NormalizeProvider(provider);
        var key = GetProviderSettingKey(normalizedProvider);

        var hasHostCredential = await HasStoredValueAsync(key, SettingScope.Global, cancellationToken: cancellationToken);
        var hasTenantOverride = await HasStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken);
        var effectiveSource = await ResolveSourceAsync(key, tenantId, cancellationToken);

        return new TextToSpeechCredentialSnapshot(normalizedProvider, hasHostCredential, hasTenantOverride, effectiveSource);
    }

    public async Task<TextToSpeechCredentialSnapshot> SaveTenantAsync(
        TextToSpeechCredentialUpdate update,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalizedProvider = NormalizeProvider(update.Provider);
        var key = GetProviderSettingKey(normalizedProvider);
        await SaveAsync(key, SettingScope.Tenant, tenantId, update, cancellationToken);
        return await GetTenantAsync(normalizedProvider, cancellationToken);
    }

    public async Task<TextToSpeechProviderCredentialResolution> ResolveAsync(
        string provider,
        CancellationToken cancellationToken = default)
    {
        var normalizedProvider = NormalizeProvider(provider);
        var key = GetProviderSettingKey(normalizedProvider);
        var tenantId = _tenantProvider.TryGetCurrentTenantId(out var resolvedTenantId) ? resolvedTenantId : (Guid?)null;

        if (tenantId.HasValue)
        {
            var tenantCredential = await GetStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(tenantCredential))
            {
                return new TextToSpeechProviderCredentialResolution(
                    normalizedProvider,
                    tenantCredential,
                    "TenantOverride",
                    HasCredential: true,
                    IsTenantOverride: true);
            }
        }

        var hostCredential = await GetStoredValueAsync(key, SettingScope.Global, null, cancellationToken);
        if (!string.IsNullOrWhiteSpace(hostCredential))
        {
            return new TextToSpeechProviderCredentialResolution(
                normalizedProvider,
                hostCredential,
                "HostDefault",
                HasCredential: true,
                IsTenantOverride: false);
        }

        var configFallback = GetConfigurationFallback(normalizedProvider);
        return new TextToSpeechProviderCredentialResolution(
            normalizedProvider,
            configFallback,
            string.IsNullOrWhiteSpace(configFallback) ? "Missing" : "Configuration",
            HasCredential: !string.IsNullOrWhiteSpace(configFallback),
            IsTenantOverride: false);
    }

    private async Task SaveAsync(
        string key,
        SettingScope scope,
        Guid? tenantId,
        TextToSpeechCredentialUpdate update,
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

        if (string.IsNullOrWhiteSpace(normalizedApiKey))
        {
            return;
        }

        var protectedValue = _protector.Protect(normalizedApiKey);
        if (existing == null)
        {
            existing = new Setting
            {
                Key = key,
                Scope = scope,
                TenantId = tenantId,
                UserId = null,
                Value = protectedValue
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

        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return null;
        }

        return _protector.Unprotect(storedValue);
    }

    private async Task<string> ResolveSourceAsync(
        string key,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId.HasValue && await HasStoredValueAsync(key, SettingScope.Tenant, tenantId, cancellationToken))
        {
            return "TenantOverride";
        }

        if (await HasStoredValueAsync(key, SettingScope.Global, cancellationToken: cancellationToken))
        {
            return "HostDefault";
        }

        return string.IsNullOrWhiteSpace(GetConfigurationFallbackFromKey(key)) ? "Missing" : "Configuration";
    }

    private string? GetConfigurationFallback(string provider)
    {
        // Convention: AI:TextToSpeech:{Provider}ApiKey  (e.g. ElevenLabsApiKey, MistralApiKey)
        return _configuration[$"AI:TextToSpeech:{provider}ApiKey"];
    }

    private string? GetConfigurationFallbackFromKey(string key)
    {
        // Reverse-map the setting key to a provider name, then use the standard fallback.
        // Key format: Platform.TextToSpeech.Providers.{Provider}.ApiKey
        const string prefix = "Platform.TextToSpeech.Providers.";
        const string suffix = ".ApiKey";
        if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            && key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            var provider = key[prefix.Length..^suffix.Length];
            return GetConfigurationFallback(provider);
        }

        return null;
    }

    private static string GetProviderSettingKey(string provider)
    {
        return TextToSpeechSettingNames.GetProviderApiKeySettingName(provider);
    }

    private static string NormalizeProvider(string provider)
    {
        return string.IsNullOrWhiteSpace(provider) ? "ElevenLabs" : provider.Trim();
    }
}
