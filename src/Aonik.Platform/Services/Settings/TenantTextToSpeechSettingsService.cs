using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Settings;
using Aonik.Platform.Persistence;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Settings;
using ZiggyCreatures.Caching.Fusion;

namespace Aonik.Platform.Services.Settings;

internal sealed class TenantTextToSpeechSettingsService : ITenantTextToSpeechSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    // The TTS profile is read 10+ times in a single voice run (once per
    // synth chunk plus AGUI preflight). It changes only when an admin
    // saves new settings, so a 10-minute TTL with fail-safe is plenty.
    // The trace audit showed 13 reads of this key per request before
    // caching — see traces/02d2d74bb4a3 in dev for the baseline.
    private static readonly FusionCacheEntryOptions CacheEntryOptions = new(TimeSpan.FromMinutes(10))
    {
        IsFailSafeEnabled = true,
        FailSafeMaxDuration = TimeSpan.FromHours(1),
    };

    private const string CacheKeyPrefix = "tts-settings:v1:";

    private readonly PlatformDbContext _dbContext;
    private readonly ITenantProvider _tenantProvider;
    private readonly IFusionCache _cache;

    public TenantTextToSpeechSettingsService(
        PlatformDbContext dbContext,
        ITenantProvider tenantProvider,
        IFusionCache cache)
    {
        _dbContext = dbContext;
        _tenantProvider = tenantProvider;
        _cache = cache;
    }

    private static string CacheKey(Guid tenantId) => $"{CacheKeyPrefix}{tenantId:N}";

    public async Task<TextToSpeechSettings> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();

        return await _cache.GetOrSetAsync<TextToSpeechSettings>(
            CacheKey(tenantId),
            async ct => await LoadFromDbAsync(tenantId, ct),
            CacheEntryOptions,
            cancellationToken);
    }

    private async Task<TextToSpeechSettings> LoadFromDbAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var payload = await _dbContext.Settings
            .AsNoTracking()
            .Where(setting => setting.Key == TextToSpeechSettingNames.TenantProfile
                              && setting.Scope == SettingScope.Tenant
                              && setting.TenantId == tenantId)
            .Select(setting => setting.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(payload))
        {
            return CreateDefault();
        }

        var document = JsonSerializer.Deserialize<TextToSpeechSettingsDocument>(payload, SerializerOptions);
        return document == null ? CreateDefault() : Normalize(Map(document));
    }

    public async Task<TextToSpeechSettings> SaveCurrentAsync(
        TextToSpeechSettings settings,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetCurrentTenantId();
        var normalized = Normalize(settings);
        var document = ToDocument(normalized);
        var payload = JsonSerializer.Serialize(document, SerializerOptions);

        var existing = await _dbContext.Settings.FirstOrDefaultAsync(
            setting => setting.Key == TextToSpeechSettingNames.TenantProfile
                       && setting.Scope == SettingScope.Tenant
                       && setting.TenantId == tenantId,
            cancellationToken);

        if (existing == null)
        {
            existing = new Setting
            {
                Key = TextToSpeechSettingNames.TenantProfile,
                Scope = SettingScope.Tenant,
                TenantId = tenantId,
                Value = payload
            };
            _dbContext.Settings.Add(existing);
        }
        else
        {
            existing.Value = payload;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate the cached profile so the next reader picks up the
        // new settings on the immediately following request.
        await _cache.RemoveAsync(CacheKey(tenantId), token: cancellationToken);

        return normalized;
    }

    private static TextToSpeechSettings CreateDefault()
    {
        return new TextToSpeechSettings(
            Enabled: false,
            FallbackToNativeOnFailure: true,
            DefaultProfile: new TextToSpeechVoiceProfile(
                Provider: "ElevenLabs",
                VoiceId: string.Empty,
                ModelId: "eleven_multilingual_v2",
                Locale: "en-US",
                OutputFormat: "mp3_44100_128",
                ProviderOptions: new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)),
            Policy: new TextToSpeechPolicy(
                MaxCharactersPerUtterance: 280,
                MaxRequestsPerMinutePerUser: 20,
                MonthlyCharacterBudget: null));
    }

    private static TextToSpeechSettings Map(TextToSpeechSettingsDocument document)
    {
        return new TextToSpeechSettings(
            document.Enabled,
            document.FallbackToNativeOnFailure,
            new TextToSpeechVoiceProfile(
                document.DefaultProfile.Provider ?? string.Empty,
                document.DefaultProfile.VoiceId ?? string.Empty,
                document.DefaultProfile.ModelId,
                document.DefaultProfile.Locale,
                document.DefaultProfile.OutputFormat,
                document.DefaultProfile.ProviderOptions ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)),
            new TextToSpeechPolicy(
                document.Policy.MaxCharactersPerUtterance,
                document.Policy.MaxRequestsPerMinutePerUser,
                document.Policy.MonthlyCharacterBudget));
    }

    private static TextToSpeechSettingsDocument ToDocument(TextToSpeechSettings settings)
    {
        return new TextToSpeechSettingsDocument(
            settings.Enabled,
            settings.FallbackToNativeOnFailure,
            new TextToSpeechVoiceProfileDocument(
                settings.DefaultProfile.Provider,
                settings.DefaultProfile.VoiceId,
                settings.DefaultProfile.ModelId,
                settings.DefaultProfile.Locale,
                settings.DefaultProfile.OutputFormat,
                new Dictionary<string, string?>(settings.DefaultProfile.ProviderOptions, StringComparer.OrdinalIgnoreCase)),
            new TextToSpeechPolicyDocument(
                settings.Policy.MaxCharactersPerUtterance,
                settings.Policy.MaxRequestsPerMinutePerUser,
                settings.Policy.MonthlyCharacterBudget));
    }

    private static TextToSpeechSettings Normalize(TextToSpeechSettings settings)
    {
        var provider = string.IsNullOrWhiteSpace(settings.DefaultProfile.Provider)
            ? "ElevenLabs"
            : settings.DefaultProfile.Provider.Trim();
        var voiceId = string.IsNullOrWhiteSpace(settings.DefaultProfile.VoiceId)
            ? string.Empty
            : settings.DefaultProfile.VoiceId.Trim();
        var modelId = string.IsNullOrWhiteSpace(settings.DefaultProfile.ModelId)
            ? "eleven_multilingual_v2"
            : settings.DefaultProfile.ModelId.Trim();
        var locale = string.IsNullOrWhiteSpace(settings.DefaultProfile.Locale)
            ? "en-US"
            : settings.DefaultProfile.Locale.Trim();
        var outputFormat = string.IsNullOrWhiteSpace(settings.DefaultProfile.OutputFormat)
            ? "mp3_44100_128"
            : settings.DefaultProfile.OutputFormat.Trim();

        var options = settings.DefaultProfile.ProviderOptions
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(
                pair => pair.Key.Trim(),
                pair => string.IsNullOrWhiteSpace(pair.Value) ? null : pair.Value.Trim(),
                StringComparer.OrdinalIgnoreCase);

        var maxCharacters = settings.Policy.MaxCharactersPerUtterance <= 0
            ? 280
            : Math.Min(settings.Policy.MaxCharactersPerUtterance, 2000);
        var maxRequestsPerMinute = settings.Policy.MaxRequestsPerMinutePerUser <= 0
            ? 20
            : Math.Min(settings.Policy.MaxRequestsPerMinutePerUser, 120);
        var monthlyBudget = settings.Policy.MonthlyCharacterBudget.HasValue && settings.Policy.MonthlyCharacterBudget.Value <= 0
            ? null
            : settings.Policy.MonthlyCharacterBudget;

        return new TextToSpeechSettings(
            settings.Enabled,
            settings.FallbackToNativeOnFailure,
            new TextToSpeechVoiceProfile(provider, voiceId, modelId, locale, outputFormat, options),
            new TextToSpeechPolicy(maxCharacters, maxRequestsPerMinute, monthlyBudget));
    }

    private sealed record TextToSpeechSettingsDocument(
        bool Enabled,
        bool FallbackToNativeOnFailure,
        TextToSpeechVoiceProfileDocument DefaultProfile,
        TextToSpeechPolicyDocument Policy);

    private sealed record TextToSpeechVoiceProfileDocument(
        string? Provider,
        string? VoiceId,
        string? ModelId,
        string? Locale,
        string? OutputFormat,
        Dictionary<string, string?>? ProviderOptions);

    private sealed record TextToSpeechPolicyDocument(
        int MaxCharactersPerUtterance,
        int MaxRequestsPerMinutePerUser,
        int? MonthlyCharacterBudget);
}
