namespace Aonik.Ai.Services;

internal sealed class TextToSpeechOptions
{
    public string ElevenLabsBaseUrl { get; set; } = "https://api.elevenlabs.io";
    public string? ElevenLabsApiKey { get; set; }

    public string MistralBaseUrl { get; set; } = "https://api.mistral.ai";
    public string? MistralApiKey { get; set; }

    /// <summary>
    /// Per-request HTTP timeout (seconds) applied to TTS provider HttpClients.
    /// Defaults to 60s — long enough for a long sentence to synthesise on a
    /// healthy provider, short enough to fail fast when a provider hangs.
    /// Without an explicit value an HttpClient inherits the .NET default of
    /// 100s, which is too generous for an interactive voice surface.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;
}
