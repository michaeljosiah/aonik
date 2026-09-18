using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Settings;
using Microsoft.Extensions.Configuration;

namespace Aonik.Infrastructure.Settings;

/// <summary>
/// Resolves AI provider settings from the Settings module, tenant first: the tenant is the product
/// (Arke Kidz, Simi, …), and the tenant's operator configures the provider and key their product
/// runs on, encrypted and rotated from the Admin UI. A tenant that has set nothing runs on the
/// platform's global values, then the configuration seed, then the defaults — the same rule the
/// content-safety route applies to the same key, so one tenant's OpenAI is one key everywhere.
/// <para>
/// Scoped — all settings resolved once during construction. The legacy <see cref="IConfiguration"/>
/// fallback stays for appsettings-based deployments that predate the Settings module.
/// </para>
/// </summary>
internal sealed class AiProviderSettings : IAiProviderSettings
{
    public string Provider { get; }
    public string? OpenAiApiKey { get; }
    public string OpenAiModel { get; }
    public string OpenAiImageModel { get; }

    public AiProviderSettings(TenantFirstSettingReader settings, IConfiguration configuration)
    {
        Provider = ResolveSync(settings, AiSettingNames.Provider)
            ?? configuration["AI:Provider"]
            ?? "Stub";

        OpenAiApiKey = ResolveSync(settings, AiSettingNames.OpenAiApiKey)
            ?? configuration["AI:OpenAI:ApiKey"];

        OpenAiModel = ResolveSync(settings, AiSettingNames.OpenAiModel)
            ?? configuration["AI:OpenAI:Model"]
            ?? "gpt-5-mini";

        OpenAiImageModel = ResolveSync(settings, AiSettingNames.OpenAiImageModel)
            ?? configuration["AI:OpenAI:ImageModel"]
            ?? "dall-e-3";
    }

    /// <summary>
    /// Synchronously resolve a setting. Acceptable here because:
    /// 1. Settings are cached (FusionCache) — no DB hit after first resolution
    /// 2. Runs once per scope (constructor)
    /// 3. Follows the same pattern as the UserMemoryBackend factory in DI
    /// </summary>
    private static string? ResolveSync(TenantFirstSettingReader settings, string key)
        => settings.ReadAsync(key).GetAwaiter().GetResult();
}
