using Aonik.Platform.Contracts.Services.Settings;
using Aonik.Platform.Settings;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Extensions.Configuration;

namespace Aonik.Infrastructure.Settings;

/// <summary>
/// Resolves AI provider settings from the Settings module (database-backed),
/// with fallback to <see cref="IConfiguration"/> for backward compatibility
/// with existing appsettings-based deployments.
/// <para>
/// Scoped — all settings resolved once during construction.
/// </para>
/// </summary>
internal sealed class AiProviderSettings : IAiProviderSettings
{
    public string Provider { get; }
    public string? OpenAiApiKey { get; }
    public string OpenAiModel { get; }
    public string OpenAiImageModel { get; }

    public AiProviderSettings(
        ISettingProvider settingProvider,
        IConfiguration configuration)
    {
        // Resolve each setting: Settings module first, then legacy IConfiguration fallback.
        // The Settings module's own resolution chain is: DB (User→Tenant→Global) → Config → Default.
        // The extra IConfiguration fallback here covers the old "AI:Provider" key format
        // (colon-separated, uppercase) that won't match the Settings module's "Ai.Provider" key.

        Provider = ResolveSync(settingProvider, AiSettingNames.Provider)
            ?? configuration["AI:Provider"]
            ?? "Stub";

        OpenAiApiKey = ResolveSync(settingProvider, AiSettingNames.OpenAiApiKey)
            ?? configuration["AI:OpenAI:ApiKey"];

        OpenAiModel = ResolveSync(settingProvider, AiSettingNames.OpenAiModel)
            ?? configuration["AI:OpenAI:Model"]
            ?? "gpt-5-mini";

        OpenAiImageModel = ResolveSync(settingProvider, AiSettingNames.OpenAiImageModel)
            ?? configuration["AI:OpenAI:ImageModel"]
            ?? "dall-e-3";
    }

    /// <summary>
    /// Synchronously resolve a setting. Acceptable here because:
    /// 1. Settings are cached (FusionCache) — no DB hit after first resolution
    /// 2. Runs once per scope (constructor)
    /// 3. Follows the same pattern as the UserMemoryBackend factory in DI
    /// </summary>
    private static string? ResolveSync(ISettingProvider settingProvider, string key)
        => settingProvider.GetAsync(key).GetAwaiter().GetResult();
}
