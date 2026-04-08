using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Aonik.Ai.Services;
using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Ai.Providers;

internal sealed class MistralTextToSpeechProvider : ITextToSpeechProvider
{
    private const string DefaultModel = "voxtral-mini-tts-2603";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Dictionary<string, string> OutputFormatContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp3"] = "audio/mpeg",
        ["wav"] = "audio/wav",
        ["pcm"] = "audio/pcm",
        ["flac"] = "audio/flac",
        ["opus"] = "audio/opus"
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<MistralTextToSpeechProvider> _logger;

    public MistralTextToSpeechProvider(
        HttpClient httpClient,
        ILogger<MistralTextToSpeechProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public string Name => "Mistral";

    public bool SupportsVoiceCreation => true;

    public async Task<TextToSpeechProviderStreamResult> SynthesizeAsync(
        TextToSpeechProviderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("Mistral API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.VoiceId))
        {
            throw new InvalidOperationException("Voice ID is required for Mistral text-to-speech.");
        }

        var outputFormat = NormalizeOutputFormat(request.OutputFormat);
        var modelId = string.IsNullOrWhiteSpace(request.ModelId) ? DefaultModel : request.ModelId;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/audio/speech")
        {
            Content = JsonContent.Create(new MistralSpeechRequest(
                modelId,
                request.Text,
                request.VoiceId,
                outputFormat), options: SerializerOptions)
        };

        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Mistral TTS request failed with status {StatusCode}: {ErrorBody}",
                (int)statusCode,
                TextToSpeechProviderHelpers.Truncate(errorBody));
            response.Dispose();
            throw new InvalidOperationException(BuildErrorMessage("text-to-speech", statusCode, errorBody));
        }

        // Mistral returns base64-encoded audio in JSON rather than a raw stream.
        var payload = await response.Content.ReadFromJsonAsync<MistralSpeechResponse>(SerializerOptions, cancellationToken);
        response.Dispose();

        if (payload == null || string.IsNullOrWhiteSpace(payload.AudioData))
        {
            throw new InvalidOperationException("Mistral TTS returned an empty response.");
        }

        var audioBytes = Convert.FromBase64String(payload.AudioData);
        var audioStream = new MemoryStream(audioBytes, writable: false);

        var contentType = OutputFormatContentTypes.GetValueOrDefault(outputFormat, "audio/mpeg");

        return new TextToSpeechProviderStreamResult(
            audioStream,
            contentType,
            Name,
            request.VoiceId,
            modelId);
    }

    public async Task<IReadOnlyList<TextToSpeechVoiceOption>> GetVoicesAsync(
        string? apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return Array.Empty<TextToSpeechVoiceOption>();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/audio/voices?limit=100");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Mistral voice list request failed with status {StatusCode}: {ErrorBody}",
                (int)response.StatusCode,
                TextToSpeechProviderHelpers.Truncate(errorBody));
            throw new InvalidOperationException(BuildErrorMessage("voice list", response.StatusCode, errorBody));
        }

        var payload = await response.Content.ReadFromJsonAsync<MistralVoicesResponse>(SerializerOptions, cancellationToken);
        if (payload?.Items == null || payload.Items.Count == 0)
        {
            return Array.Empty<TextToSpeechVoiceOption>();
        }

        return payload.Items
            .Where(voice => !string.IsNullOrWhiteSpace(voice.Id))
            .Select(voice =>
            {
                var labels = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(voice.Gender))
                    labels["gender"] = voice.Gender;
                if (voice.Age.HasValue)
                    labels["age"] = voice.Age.Value.ToString();
                if (!string.IsNullOrWhiteSpace(voice.Slug))
                    labels["slug"] = voice.Slug;
                if (voice.Languages is { Count: > 0 })
                    labels["languages"] = string.Join(", ", voice.Languages);
                if (voice.Tags is { Count: > 0 })
                    labels["tags"] = string.Join(", ", voice.Tags);

                return new TextToSpeechVoiceOption(
                    voice.Id!,
                    string.IsNullOrWhiteSpace(voice.Name) ? voice.Id! : voice.Name!,
                    PreviewUrl: null,
                    Category: voice.Gender,
                    labels);
            })
            .ToArray();
    }

    public async Task<TextToSpeechCreateVoiceResult> CreateVoiceAsync(
        TextToSpeechCreateVoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("Mistral API key is not configured.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Voice name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.SampleAudioBase64))
        {
            throw new ArgumentException("Sample audio is required for voice creation.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/audio/voices")
        {
            Content = JsonContent.Create(new MistralCreateVoiceRequest(
                request.Name,
                request.SampleAudioBase64,
                request.SampleFilename,
                request.Languages,
                request.Gender,
                request.Age,
                request.Tags), options: SerializerOptions)
        };

        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Mistral create voice request failed with status {StatusCode}: {ErrorBody}",
                (int)response.StatusCode,
                TextToSpeechProviderHelpers.Truncate(errorBody));
            throw new InvalidOperationException(BuildErrorMessage("voice creation", response.StatusCode, errorBody));
        }

        var payload = await response.Content.ReadFromJsonAsync<MistralVoice>(SerializerOptions, cancellationToken);
        if (payload == null || string.IsNullOrWhiteSpace(payload.Id))
        {
            throw new InvalidOperationException("Mistral returned an empty voice creation response.");
        }

        return new TextToSpeechCreateVoiceResult(payload.Id!, payload.Name ?? request.Name);
    }

    public async Task DeleteVoiceAsync(
        TextToSpeechDeleteVoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ApiKey))
        {
            throw new InvalidOperationException("Mistral API key is not configured.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Delete,
            $"/v1/audio/voices/{Uri.EscapeDataString(request.VoiceId)}");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", request.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Mistral delete voice request failed with status {StatusCode}: {ErrorBody}",
                (int)response.StatusCode,
                TextToSpeechProviderHelpers.Truncate(errorBody));
            throw new InvalidOperationException(BuildErrorMessage("voice deletion", response.StatusCode, errorBody));
        }
    }

    private static string NormalizeOutputFormat(string? outputFormat)
    {
        if (string.IsNullOrWhiteSpace(outputFormat))
        {
            return "mp3";
        }

        var normalized = outputFormat.Trim().ToLowerInvariant();

        // Map ElevenLabs-style format names to Mistral equivalents so tenants switching
        // providers don't need to change their stored output format.
        if (normalized.StartsWith("mp3", StringComparison.Ordinal))
            return "mp3";
        if (normalized.StartsWith("pcm", StringComparison.Ordinal))
            return "pcm";

        return OutputFormatContentTypes.ContainsKey(normalized) ? normalized : "mp3";
    }

    private static string BuildErrorMessage(string operation, System.Net.HttpStatusCode statusCode, string? errorBody)
        => TextToSpeechProviderHelpers.BuildErrorMessage("Mistral", operation, statusCode, errorBody);

    // ── Request/Response DTOs ────────────────────────────────────────

    private sealed record MistralSpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice_id")] string VoiceId,
        [property: JsonPropertyName("response_format")] string ResponseFormat);

    private sealed record MistralSpeechResponse(
        [property: JsonPropertyName("audio_data")] string? AudioData);

    private sealed record MistralCreateVoiceRequest(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("sample_audio")] string SampleAudio,
        [property: JsonPropertyName("sample_filename")] string? SampleFilename,
        [property: JsonPropertyName("languages")] IReadOnlyList<string>? Languages,
        [property: JsonPropertyName("gender")] string? Gender,
        [property: JsonPropertyName("age")] int? Age,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags);

    private sealed record MistralVoicesResponse(
        [property: JsonPropertyName("items")] List<MistralVoice>? Items,
        [property: JsonPropertyName("total")] int Total);

    private sealed record MistralVoice(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("slug")] string? Slug,
        [property: JsonPropertyName("languages")] List<string>? Languages,
        [property: JsonPropertyName("gender")] string? Gender,
        [property: JsonPropertyName("age")] int? Age,
        [property: JsonPropertyName("tags")] List<string>? Tags,
        [property: JsonPropertyName("created_at")] string? CreatedAt);
}
