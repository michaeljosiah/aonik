namespace Aonik.Ai.Services;

internal sealed class TextToSpeechOptions
{
    public string ElevenLabsBaseUrl { get; set; } = "https://api.elevenlabs.io";
    public string? ElevenLabsApiKey { get; set; }
}
