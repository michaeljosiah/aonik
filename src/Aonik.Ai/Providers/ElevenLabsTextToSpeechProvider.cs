using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Providers;

internal sealed class ElevenLabsTextToSpeechProvider : ITextToSpeechProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly TextToSpeechOptions _options;
    private readonly ILogger<ElevenLabsTextToSpeechProvider> _logger;

    public ElevenLabsTextToSpeechProvider(
        HttpClient httpClient,
        IOptions<TextToSpeechOptions> options,
        ILogger<ElevenLabsTextToSpeechProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public string Name => "ElevenLabs";

    public async Task<TextToSpeechProviderStreamResult> SynthesizeAsync(
        TextToSpeechProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("ElevenLabs API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.VoiceId))
        {
            throw new InvalidOperationException("Voice ID is required for ElevenLabs text-to-speech.");
        }

        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.OutputFormat))
        {
            // ElevenLabs's `output_format` query param doesn't accept bare codecs like
            // "mp3" or "wav" — it requires a SKU that pins sample rate + bitrate
            // (e.g. "mp3_44100_128", "wav_24000"). Callers that just want "give me an
            // MP3" historically pass "mp3" and we got 400'd. Map the short codes to
            // sensible defaults so callers don't have to know ElevenLabs's SKU grid.
            var resolved = ResolveElevenLabsOutputFormat(request.OutputFormat);
            query.Add($"output_format={Uri.EscapeDataString(resolved)}");
        }

        if (request.ProviderOptions.TryGetValue("optimizeStreamingLatency", out var optimizeLatency)
            && !string.IsNullOrWhiteSpace(optimizeLatency))
        {
            query.Add($"optimize_streaming_latency={Uri.EscapeDataString(optimizeLatency)}");
        }

        var path = $"/v1/text-to-speech/{Uri.EscapeDataString(request.VoiceId)}/stream";
        if (query.Count > 0)
        {
            path = $"{path}?{string.Join("&", query)}";
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new ElevenLabsStreamRequest(
                request.Text,
                string.IsNullOrWhiteSpace(request.ModelId) ? "eleven_multilingual_v2" : request.ModelId,
                NormalizeLanguageCode(request.Locale),
                BuildVoiceSettings(request.ProviderOptions),
                request.PreviousText,
                request.NextText))
        };

        httpRequest.Headers.Add("xi-api-key", request.ApiKey);

        var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "ElevenLabs TTS request failed with status {StatusCode}: {ErrorBody}",
                (int)response.StatusCode,
                TextToSpeechProviderHelpers.Truncate(errorBody));
            response.Dispose();
            throw new InvalidOperationException(BuildErrorMessage("text-to-speech", response.StatusCode, errorBody));
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var contentType = response.Content.Headers.ContentType?.MediaType;

        return new TextToSpeechProviderStreamResult(
            stream,
            string.IsNullOrWhiteSpace(contentType) ? "audio/mpeg" : contentType,
            Name,
            request.VoiceId,
            request.ModelId,
            response);
    }

    public async Task<IReadOnlyList<TextToSpeechVoiceOption>> GetVoicesAsync(
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Array.Empty<TextToSpeechVoiceOption>();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v2/voices?page_size=100");
        request.Headers.Add("xi-api-key", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "ElevenLabs voice list request failed with status {StatusCode}: {ErrorBody}",
                (int)response.StatusCode,
                TextToSpeechProviderHelpers.Truncate(errorBody));
            throw new InvalidOperationException(BuildErrorMessage("voice list", response.StatusCode, errorBody));
        }

        var payload = await response.Content.ReadFromJsonAsync<ElevenLabsVoicesResponse>(SerializerOptions, cancellationToken);
        if (payload?.Voices == null || payload.Voices.Count == 0)
        {
            return Array.Empty<TextToSpeechVoiceOption>();
        }

        return payload.Voices
            .Where(voice => !string.IsNullOrWhiteSpace(voice.VoiceId))
            .Select(voice => new TextToSpeechVoiceOption(
                voice.VoiceId!,
                string.IsNullOrWhiteSpace(voice.Name) ? voice.VoiceId! : voice.Name!,
                voice.PreviewUrl,
                voice.Category,
                voice.Labels ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static string? NormalizeLanguageCode(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        var normalized = locale.Trim();
        var separatorIndex = normalized.IndexOfAny(['-', '_']);
        if (separatorIndex <= 0)
        {
            return normalized.ToLowerInvariant();
        }

        return normalized[..separatorIndex].ToLowerInvariant();
    }

    private static ElevenLabsVoiceSettings? BuildVoiceSettings(Dictionary<string, string?> providerOptions)
    {
        static double? ParseDouble(Dictionary<string, string?> values, string key)
        {
            return values.TryGetValue(key, out var raw) && double.TryParse(raw, out var parsed)
                ? parsed
                : null;
        }

        static bool? ParseBool(Dictionary<string, string?> values, string key)
        {
            return values.TryGetValue(key, out var raw) && bool.TryParse(raw, out var parsed)
                ? parsed
                : null;
        }

        var settings = new ElevenLabsVoiceSettings(
            ParseDouble(providerOptions, "stability"),
            ParseDouble(providerOptions, "similarityBoost"),
            ParseDouble(providerOptions, "style"),
            ParseDouble(providerOptions, "speed"),
            ParseBool(providerOptions, "useSpeakerBoost"));

        return settings.IsEmpty ? null : settings;
    }

    /// <summary>
    /// Translate the cross-provider short codes our callers use (<c>mp3</c>, <c>wav</c>,
    /// <c>pcm</c>) into the ElevenLabs SKU grid. Anything that already looks like a SKU
    /// (contains an underscore) passes through unchanged so admins who deliberately picked
    /// e.g. <c>pcm_44100</c> on a recipe still get exactly that.
    /// </summary>
    internal static string ResolveElevenLabsOutputFormat(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.Contains('_', StringComparison.Ordinal))
        {
            return trimmed;
        }
        return trimmed.ToLowerInvariant() switch
        {
            // mp3_44100_128 is ElevenLabs's documented "balanced" default — good quality,
            // small payload, plays directly in every browser's <audio> element.
            "mp3" => "mp3_44100_128",
            // wav_24000 keeps PCM aligned with the AONIK pipeline's 24 kHz sink.
            "wav" => "wav_24000",
            // pcm_24000 mirrors the pipeline default.
            "pcm" => "pcm_24000",
            // opus_48000_128 if a caller asks for opus.
            "opus" => "opus_48000_128",
            // Unknown short code — pass through and let ElevenLabs surface the 400 with
            // its catalogue of accepted SKUs (which is the most useful error anyway).
            _ => trimmed,
        };
    }

    private static string BuildErrorMessage(string operation, System.Net.HttpStatusCode statusCode, string? errorBody)
        => TextToSpeechProviderHelpers.BuildErrorMessage("ElevenLabs", operation, statusCode, errorBody);

    private sealed record ElevenLabsStreamRequest(
        string Text,
        string? ModelId,
        string? LanguageCode,
        ElevenLabsVoiceSettings? VoiceSettings,
        string? PreviousText,
        string? NextText);

    private sealed record ElevenLabsVoiceSettings(
        double? Stability,
        double? SimilarityBoost,
        double? Style,
        double? Speed,
        bool? UseSpeakerBoost)
    {
        public bool IsEmpty => Stability == null
                               && SimilarityBoost == null
                               && Style == null
                               && Speed == null
                               && UseSpeakerBoost == null;
    }

    private sealed record ElevenLabsVoicesResponse(List<ElevenLabsVoice>? Voices);

    private sealed record ElevenLabsVoice(
        [property: JsonPropertyName("voice_id")]
        string? VoiceId,
        string? Name,
        [property: JsonPropertyName("preview_url")]
        string? PreviewUrl,
        string? Category,
        Dictionary<string, string?>? Labels);
}
