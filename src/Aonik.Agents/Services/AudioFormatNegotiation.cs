namespace Aonik.Agents.Services;

/// <summary>
/// Maps the abstract <c>audioFormat</c> request value (<c>mp3 | opus |
/// wav</c>) to the provider-specific format string consumed by
/// <c>ITextToSpeechProvider</c> implementations. Validation runs before
/// SSE starts so unsupported combinations surface as plain HTTP 400
/// rather than a half-streamed SSE response.
/// </summary>
internal static class AudioFormatNegotiation
{
    public const string DefaultAbstractFormat = "mp3";

    /// <summary>
    /// Returns <c>true</c> when <paramref name="abstractFormat"/> is one
    /// of the documented voice-mode formats. Out-band formats (e.g.
    /// ElevenLabs' <c>mp3_44100_128</c> tenant default string) MUST go
    /// through tenant settings, never through this request field.
    /// </summary>
    public static bool IsKnownAbstractFormat(string? abstractFormat) =>
        abstractFormat is "mp3" or "opus" or "wav";

    /// <summary>
    /// Resolve the provider-specific output format string. Returns
    /// <c>null</c> when the (provider, abstract format) pair has no
    /// supported mapping — caller must surface HTTP 400.
    /// </summary>
    public static string? MapToProviderFormat(string providerName, string abstractFormat) =>
        (providerName, abstractFormat) switch
        {
            // ElevenLabs accepts a richer family of strings; pin specific
            // bitrate/sample-rate combos that are widely supported by
            // mobile decoders so we don't have to think about them.
            ("ElevenLabs", "mp3")  => "mp3_44100_128",
            ("ElevenLabs", "opus") => "opus_48000_64",
            ("ElevenLabs", "wav")  => "pcm_44100",

            // Mistral normalises to the bare extension internally.
            ("Mistral", "mp3")  => "mp3",
            ("Mistral", "opus") => "opus",
            ("Mistral", "wav")  => "wav",

            _ => null,
        };

    public static string MapAbstractToMime(string abstractFormat) =>
        abstractFormat switch
        {
            "mp3"  => "audio/mpeg",
            "opus" => "audio/opus",
            "wav"  => "audio/wav",
            _      => "application/octet-stream",
        };
}
