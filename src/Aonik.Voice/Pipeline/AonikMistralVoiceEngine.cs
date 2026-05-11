using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Voxa.Speech;

namespace Aonik.Voice.Pipeline;

/// <summary>
/// Aonik-owned replacement for <c>Voxa.Speech.Mistral.MistralTextToSpeechEngine</c>.
///
/// <para>
/// Voxa's 0.4.0-alpha Mistral engine posts to <c>/v1/audio/speech</c> with
/// <c>response_format = "pcm"</c> and then reads the response body as raw PCM bytes.
/// Mistral, however, only returns audio over its <strong>Server-Sent Events</strong>
/// channel — even when the OpenAI-compatible <c>response_format</c> is set, the
/// response is a <c>text/event-stream</c> of <c>data: {"audio_data":"&lt;base64&gt;"}</c>
/// lines (followed by <c>data: [DONE]</c>). Voxa's engine treats those SSE bytes
/// as PCM samples, which is exactly what produces the "horribly garbled, sounds
/// like static" symptom we hit on the live test card.
/// </para>
///
/// <para>
/// This engine talks to Mistral the same way <c>Aonik.Ai.Providers.MistralTextToSpeechProvider</c>
/// already does for the chat-speech path (which is known-good in production):
/// it sets <c>stream = true</c>, reads the SSE stream line-by-line, parses each
/// <c>data:</c> line, base64-decodes the <c>audio_data</c> field, and yields the
/// resulting bytes. With <c>response_format = "pcm"</c> that gives us 24 kHz mono
/// signed 16-bit LE PCM — exactly the shape Voxa's <see cref="TextToSpeechProcessor"/>
/// hands to the sink.
/// </para>
///
/// <para>
/// The 8 KB chunk size mirrors Voxa's own engine for downstream parity; the only
/// difference is how we read from the HTTP response.
/// </para>
/// </summary>
internal sealed class AonikMistralVoiceEngine : ITextToSpeechEngine
{
    private const string DefaultModel = "voxtral-mini-tts-2603";
    private const string ApiBaseUrl = "https://api.mistral.ai/v1";
    private const int ChunkSize = 8 * 1024;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _apiKey;
    private readonly string _voiceId;
    private readonly string _modelId;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public AonikMistralVoiceEngine(
        string apiKey,
        string voiceId,
        string? modelId,
        HttpClient? httpClient = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Mistral API key is required.", nameof(apiKey));
        }
        if (string.IsNullOrWhiteSpace(voiceId))
        {
            throw new ArgumentException("Mistral voice id is required.", nameof(voiceId));
        }

        _apiKey = apiKey;
        _voiceId = voiceId;
        // Mirror PreviewEngineFactory/AonikVoicePipelineFactory's rewrite of the legacy
        // placeholder model id so existing recipes keep working without manual edits.
        var raw = string.IsNullOrWhiteSpace(modelId) ? DefaultModel : modelId!;
        _modelId = raw.Trim().Equals("voxtral-tts", StringComparison.OrdinalIgnoreCase)
            ? DefaultModel
            : raw;
        _http = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public async IAsyncEnumerable<byte[]> SynthesizeAsync(
        string text,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        using var req = new HttpRequestMessage(HttpMethod.Post, $"{ApiBaseUrl}/audio/speech")
        {
            Content = JsonContent.Create(new MistralSpeechRequest(
                Model: _modelId,
                Input: text,
                VoiceId: _voiceId,
                ResponseFormat: "pcm",
                Stream: true), options: SerializerOptions),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await _http
            .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        // Stream Mistral's SSE events, base64-decode each `audio_data` payload, and
        // yield in fixed-size chunks. We intentionally don't accumulate the whole
        // utterance — the downstream WebSocketAudioSink benefits from steady frame
        // arrival (no head-of-line buffering).
        var carryover = new List<byte>(ChunkSize);

        await using var network = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(network);

        while (true)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;

            var payload = line.AsSpan(5).TrimStart();
            if (payload.SequenceEqual("[DONE]")) break;

            string? base64;
            try
            {
                using var doc = JsonDocument.Parse(payload.ToString());
                if (!doc.RootElement.TryGetProperty("audio_data", out var audioProp))
                {
                    continue;
                }
                base64 = audioProp.GetString();
            }
            catch (JsonException)
            {
                // Mistral has been observed to emit ping/heartbeat lines that aren't
                // valid JSON — skip them silently rather than killing the stream.
                continue;
            }

            if (string.IsNullOrEmpty(base64)) continue;

            var bytes = Convert.FromBase64String(base64);

            // Append to carry-over and flush full 8 KB chunks, keeping even-byte
            // alignment so 16-bit samples never split across chunk boundaries.
            carryover.AddRange(bytes);
            while (carryover.Count >= ChunkSize)
            {
                var slice = new byte[ChunkSize];
                carryover.CopyTo(0, slice, 0, ChunkSize);
                carryover.RemoveRange(0, ChunkSize);
                yield return slice;
            }
        }

        // Flush the tail.
        if (carryover.Count > 0)
        {
            // Force even-byte alignment in case Mistral terminated mid-sample (shouldn't
            // happen, but cheap insurance against producing a sample with one byte).
            var tailLen = carryover.Count - (carryover.Count % 2);
            if (tailLen > 0)
            {
                var tail = new byte[tailLen];
                carryover.CopyTo(0, tail, 0, tailLen);
                yield return tail;
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    private sealed record MistralSpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice_id")] string VoiceId,
        [property: JsonPropertyName("response_format")] string ResponseFormat,
        [property: JsonPropertyName("stream")] bool Stream);
}
