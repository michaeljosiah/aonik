using System.Text.Json;
using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.EntityFrameworkCore;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Platform.Services.Settings;

/// <summary>
/// v1 implementation. Persists <see cref="VoiceProviderConfiguration"/> as a
/// single JSON payload on the existing generic <c>Settings</c> table under
/// <see cref="VoiceProviderSettingNames.TenantProfile"/> — same pattern as
/// <see cref="TenantTextToSpeechSettingsService"/>. No EF migration required.
/// </summary>
internal sealed class TenantVoiceProviderSettingsService : ITenantVoiceProviderSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // Per-connection lookup. Voice connections are long-lived but only one
    // hello per session, so this is read at most once per WS upgrade — keep
    // the TTL short so admin updates take effect quickly.
    private static readonly FusionCacheEntryOptions CacheEntryOptions =
        new(TimeSpan.FromMinutes(2))
        {
            IsFailSafeEnabled = true,
            FailSafeMaxDuration = TimeSpan.FromMinutes(15),
        };

    private const string CacheKeyPrefix = "voice-settings:v1:";

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFusionCache _cache;

    public TenantVoiceProviderSettingsService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IFusionCache cache)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _cache = cache;
    }

    private static string CacheKey(Guid tenantId) => $"{CacheKeyPrefix}{tenantId:N}";

    public async Task<VoiceProviderConfiguration> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _cache.GetOrSetAsync<VoiceProviderConfiguration>(
            CacheKey(tenantId),
            async ct => await LoadFromDbAsync(tenantId, ct),
            CacheEntryOptions,
            cancellationToken);
    }

    private async Task<VoiceProviderConfiguration> LoadFromDbAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var payload = await _dbContext.Settings
            .AsNoTracking()
            .Where(setting => setting.Key == VoiceProviderSettingNames.TenantProfile
                              && setting.Scope == SettingScope.Tenant
                              && setting.TenantId == tenantId)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return VoiceProviderConfiguration.Disabled;
        }

        try
        {
            var deserialized = JsonSerializer.Deserialize<VoiceProviderConfiguration>(payload, SerializerOptions);
            return deserialized ?? VoiceProviderConfiguration.Disabled;
        }
        catch (JsonException)
        {
            // Malformed payload — fail safe to disabled rather than crashing
            // the WS upgrade. The setting can be re-saved through the admin UI.
            return VoiceProviderConfiguration.Disabled;
        }
    }

    public async Task<VoiceProviderConfiguration> SaveCurrentAsync(
        VoiceProviderConfiguration settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var tenantId = _tenantProvider.GetCurrentTenantId();
        var payload = JsonSerializer.Serialize(settings, SerializerOptions);

        var existing = await _dbContext.Settings.FirstOrDefaultAsync(
            setting => setting.Key == VoiceProviderSettingNames.TenantProfile
                       && setting.Scope == SettingScope.Tenant
                       && setting.TenantId == tenantId,
            cancellationToken);

        if (existing == null)
        {
            existing = new Setting
            {
                Key = VoiceProviderSettingNames.TenantProfile,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
                Value = payload,
            };
            _dbContext.Settings.Add(existing);
        }
        else
        {
            existing.Value = payload;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(CacheKey(tenantId), token: cancellationToken);

        return settings;
    }
}
