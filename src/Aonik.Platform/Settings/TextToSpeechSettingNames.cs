namespace Aonik.Platform.Settings;

public static class TextToSpeechSettingNames
{
    public const string TenantProfile = "Platform.TextToSpeech.TenantProfile";
    public const string ElevenLabsApiKey = "Platform.TextToSpeech.Providers.ElevenLabs.ApiKey";
    public const string MistralApiKey = "Platform.TextToSpeech.Providers.Mistral.ApiKey";

    /// <summary>
    /// Returns the database setting key for the given provider name.
    /// Convention: <c>Platform.TextToSpeech.Providers.{Provider}.ApiKey</c>.
    /// </summary>
    public static string GetProviderApiKeySettingName(string provider)
    {
        // Use well-known constants for existing providers to avoid case drift.
        if (provider.Equals("ElevenLabs", StringComparison.OrdinalIgnoreCase))
            return ElevenLabsApiKey;
        if (provider.Equals("Mistral", StringComparison.OrdinalIgnoreCase))
            return MistralApiKey;

        // Convention-based fallback — any future provider works without code changes.
        return $"Platform.TextToSpeech.Providers.{provider}.ApiKey";
    }
}
