namespace Aonik.Platform.Settings;

/// <summary>
/// Setting keys for the Voice module's tenant configuration. Mirrors the
/// <see cref="TextToSpeechSettingNames"/> shape — voice settings persist as
/// a single JSON payload under <see cref="TenantProfile"/>.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 2.
/// </para>
/// </summary>
public static class VoiceProviderSettingNames
{
    public const string TenantProfile = "Platform.Voice.TenantProfile";

    /// <summary>
    /// Returns the database setting key for the given voice provider's API key.
    /// Convention: <c>Platform.Voice.Providers.{Provider}.ApiKey</c> — same shape
    /// as <see cref="TextToSpeechSettingNames.GetProviderApiKeySettingName"/>.
    /// </summary>
    public static string GetProviderApiKeySettingName(string provider)
        => $"Platform.Voice.Providers.{NormalizeProvider(provider)}.ApiKey";

    /// <summary>Canonical capitalisation/spelling for known voice providers.</summary>
    public static string NormalizeProvider(string provider)
    {
        if (string.IsNullOrWhiteSpace(provider)) return "OpenAI";
        var trimmed = provider.Trim();
        return trimmed.ToLowerInvariant() switch
        {
            "openai" or "openai-whisper" or "open_ai" => "OpenAI",
            "azure" or "azurespeech" or "azure-speech" or "azure_openai" or "azureopenai" => "Azure",
            "elevenlabs" or "eleven-labs" or "eleven_labs" => "ElevenLabs",
            "mistral" or "voxtral" => "Mistral",
            _ => trimmed,
        };
    }
}
