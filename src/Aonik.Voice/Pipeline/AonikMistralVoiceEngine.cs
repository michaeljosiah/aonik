using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Voxa.Speech;

namespace Aonik.Voice.Pipeline;

/// <summary>
/// Aonik-owned replacement for <c>Voxa.Speech.Mistral.MistralTextToSpeechEngine</c>.
///
/// <para>
/// Voxa's 0.4.0-alpha Mistral engine posts to <c>/v1/audio/speech</c> with
/// <c>response_format = "pcm"</c> and reads the response body as raw PCM bytes.
/// Two things go wrong in our pipeline:
/// </para>
/// <list type="number">
///   <item>Mistral returns audio only over Server-Sent Events
///       (<c>data: {"audio_data":"&lt;base64&gt;"}</c> lines terminated by
///       <c>data: [DONE]</c>) regardless of the OpenAI-compatible
///       <c>response_format</c>. Voxa reads those SSE bytes as PCM, producing
///       static.</item>
///   <item>Mistral's raw-PCM rate and bit depth are undocumented and don't match
///       Voxa's hard-coded 24 kHz / 16-bit assumption — even after the SSE fix
///       the bytes played back distorted with a slow-talking artifact, consistent
///       with a 2× sample-rate mismatch. The existing comment at
///       <c>SpeechProviderTestEndpoints.cs:109-113</c> already calls this out:
///       "vendors' raw PCM couldn't be reliably negotiated" — the chat-speech
///       test card uses MP3 + native browser playback to avoid it.</item>
/// </list>
///
/// <para>
/// This engine fixes both by requesting <strong>WAV</strong> from Mistral. WAV is
/// raw PCM with a 44-byte header that explicitly declares the sample rate, bit
/// depth, and channel count — so we know what Mistral actually produced rather
/// than guessing. The header is parsed off the first audio_data payload; the
/// remaining bytes are yielded as 16-bit signed LE PCM in the same 8 KB chunks
/// Voxa's stock engine emits.
/// </para>
///
/// <para>
/// If Mistral's WAV declares anything other than the
/// <see cref="ExpectedSampleRate"/> / <see cref="ExpectedBitsPerSample"/> /
/// <see cref="ExpectedChannels"/> contract this engine throws an
/// <see cref="InvalidOperationException"/>. That's preferable to a subtly wrong
/// playback — the failure is loud (WS closes with an error envelope) and the log
/// line tells the operator exactly what Mistral declared so we can either update
/// the contract or add a resampler.
/// </para>
/// </summary>
internal sealed class AonikMistralVoiceEngine : ITextToSpeechEngine
{
    private const string DefaultModel = "voxtral-mini-tts-2603";
    private const string ApiBaseUrl = "https://api.mistral.ai/v1";
    private const int ChunkSize = 8 * 1024;

    // Voxa's chained pipeline emits AudioRawFrames at 24 kHz 16-bit mono — clients
    // (admin UI LiveVoiceTestCard, payabo_mobile mp_audio_stream player) decode
    // raw PCM assuming that contract. If Mistral ever returns something else
    // we surface the mismatch loudly so we can decide on a resampling strategy.
    private const int ExpectedSampleRate = 24000;
    private const int ExpectedBitsPerSample = 16;
    private const int ExpectedChannels = 1;

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _apiKey;
    private readonly string _voiceId;
    private readonly string _modelId;
    private readonly string _responseFormat;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly ILogger _logger;

    public AonikMistralVoiceEngine(
        string apiKey,
        string voiceId,
        string? modelId,
        string? responseFormat = null,
        HttpClient? httpClient = null,
        ILogger? logger = null)
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
        // placeholder model id so stale recipes keep working without manual edits.
        var rawModel = string.IsNullOrWhiteSpace(modelId) ? DefaultModel : modelId!;
        _modelId = rawModel.Trim().Equals("voxtral-tts", StringComparison.OrdinalIgnoreCase)
            ? DefaultModel
            : rawModel;
        // wav (default) gives us a header to validate sample rate / bit depth; pcm
        // trusts 24 kHz / 16-bit / mono. Anything else throws because the downstream
        // sink decodes raw PCM only — we don't ship MP3/Opus decoders.
        _responseFormat = NormaliseResponseFormat(responseFormat);
        _http = httpClient ?? new HttpClient();
        _ownsHttpClient = httpClient is null;
        _logger = logger ?? NullLogger.Instance;
    }

    private static string NormaliseResponseFormat(string? raw)
    {
        var trimmed = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return trimmed switch
        {
            "" or "wav" => "wav",
            "pcm" => "pcm",
            _ => throw new ArgumentException(
                $"Mistral response_format '{raw}' is not supported. "
                + "Use 'wav' (default) or 'pcm'.",
                nameof(raw)),
        };
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
                ResponseFormat: _responseFormat,
                Stream: true), options: SerializerOptions),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var resp = await _http
            .SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();

        var carryover = new List<byte>(ChunkSize);
        // Skip header-parsing in the PCM branch — Mistral emits raw 24 kHz 16-bit
        // mono samples directly, no RIFF/WAVE wrapper. WAV (default) still parses
        // the 44-byte header off the first SSE event.
        var headerParsed = _responseFormat == "pcm";

        await using var network = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var reader = new StreamReader(network);

        int totalAudioBytes = 0;
        int sseEventCount = 0;

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
                // Mistral occasionally emits ping/heartbeat lines that aren't JSON —
                // skip rather than killing the stream.
                continue;
            }

            if (string.IsNullOrEmpty(base64)) continue;
            sseEventCount++;

            var bytes = Convert.FromBase64String(base64);
            carryover.AddRange(bytes);
            totalAudioBytes += bytes.Length;

            // First chunk should contain the RIFF/WAVE header. Parse + strip it
            // before yielding any PCM samples downstream.
            if (!headerParsed)
            {
                if (carryover.Count < 44)
                {
                    // Wait for more bytes to arrive — header may straddle the
                    // first SSE event when the response is very fragmented.
                    continue;
                }

                var headerSpan = CollectionsMarshalAsSpan(carryover, 0, 44);
                var (dataOffset, rate, bits, channels) = ParseWavHeader(headerSpan);
                _logger.LogInformation(
                    "Mistral TTS WAV header: rate={Rate} bits={Bits} channels={Channels} dataOffset={DataOffset}",
                    rate, bits, channels, dataOffset);

                if (rate != ExpectedSampleRate || bits != ExpectedBitsPerSample || channels != ExpectedChannels)
                {
                    throw new InvalidOperationException(
                        $"Mistral returned WAV at rate={rate} bits={bits} channels={channels}; "
                        + $"expected {ExpectedSampleRate}/{ExpectedBitsPerSample}/{ExpectedChannels}. "
                        + "Update AonikMistralVoiceEngine to resample if Mistral has changed its output contract.");
                }

                carryover.RemoveRange(0, dataOffset);
                headerParsed = true;
            }

            while (carryover.Count >= ChunkSize)
            {
                var slice = new byte[ChunkSize];
                carryover.CopyTo(0, slice, 0, ChunkSize);
                carryover.RemoveRange(0, ChunkSize);
                yield return slice;
            }
        }

        // Flush the tail with even-byte alignment so we never produce half a sample.
        if (headerParsed && carryover.Count > 0)
        {
            var tailLen = carryover.Count - (carryover.Count % 2);
            if (tailLen > 0)
            {
                var tail = new byte[tailLen];
                carryover.CopyTo(0, tail, 0, tailLen);
                yield return tail;
            }
        }

        _logger.LogDebug(
            "Mistral TTS synthesis done: {Events} SSE events / {Bytes} audio_data bytes (incl. WAV header)",
            sseEventCount, totalAudioBytes);
    }

    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
        return ValueTask.CompletedTask;
    }

    // ── WAV header parsing ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse a Microsoft RIFF/WAVE header. We only support PCM (audioFormat = 1)
    /// and assume a standard 44-byte layout; any deviation (extended fmt chunk,
    /// extra LIST chunks, etc.) throws. Returns (dataChunkOffset, sampleRate,
    /// bitsPerSample, channels).
    /// </summary>
    private static (int DataOffset, int SampleRate, int BitsPerSample, int Channels) ParseWavHeader(
        ReadOnlySpan<byte> header)
    {
        if (header.Length < 44)
        {
            throw new InvalidOperationException(
                $"Mistral response is too short to contain a WAV header ({header.Length} < 44 bytes).");
        }

        // "RIFF" at offset 0, "WAVE" at offset 8 — anything else means Mistral
        // didn't return WAV (maybe it ignored response_format=wav and returned
        // raw PCM or MP3).
        if (header[0] != 'R' || header[1] != 'I' || header[2] != 'F' || header[3] != 'F'
            || header[8] != 'W' || header[9] != 'A' || header[10] != 'V' || header[11] != 'E')
        {
            throw new InvalidOperationException(
                "Mistral response is not a RIFF/WAVE container — first 12 bytes: "
                + BitConverter.ToString(header.Slice(0, 12).ToArray())
                + " (\"" + Encoding.ASCII.GetString(header.Slice(0, 12).ToArray()).Replace('\0', '.') + "\")");
        }

        // "fmt " sub-chunk at offset 12. fmt chunk size at offset 16 (usually 16
        // for PCM). audioFormat at offset 20 (1 = PCM). channels at offset 22.
        // sampleRate at offset 24. bitsPerSample at offset 34.
        if (header[12] != 'f' || header[13] != 'm' || header[14] != 't' || header[15] != ' ')
        {
            throw new InvalidOperationException("Mistral WAV is missing fmt chunk at offset 12.");
        }
        var fmtChunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(16, 4));
        var audioFormat = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(20, 2));
        if (audioFormat != 1)
        {
            throw new InvalidOperationException(
                $"Mistral WAV uses audioFormat={audioFormat}; only PCM (1) is supported.");
        }
        var channels = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(22, 2));
        var sampleRate = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(24, 4));
        var bitsPerSample = BinaryPrimitives.ReadInt16LittleEndian(header.Slice(34, 2));

        // "data" sub-chunk at offset 20 + fmtChunkSize (so 36 for a standard 16-byte fmt).
        var dataChunkStart = 20 + fmtChunkSize;
        if (header.Length < dataChunkStart + 8
            || header[dataChunkStart] != 'd' || header[dataChunkStart + 1] != 'a'
            || header[dataChunkStart + 2] != 't' || header[dataChunkStart + 3] != 'a')
        {
            throw new InvalidOperationException(
                $"Mistral WAV is missing 'data' sub-chunk at offset {dataChunkStart}.");
        }
        var dataOffset = dataChunkStart + 8;
        return (dataOffset, sampleRate, bitsPerSample, channels);
    }

    private static ReadOnlySpan<byte> CollectionsMarshalAsSpan(List<byte> list, int start, int length)
    {
        // List<byte> doesn't expose its internal buffer publicly, but we can
        // materialise a span via the CopyTo+ToArray fallback. Cheap because we
        // only do this once per synthesis (44 bytes for the header parse).
        var buf = new byte[length];
        list.CopyTo(start, buf, 0, length);
        return buf;
    }

    private sealed record MistralSpeechRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("input")] string Input,
        [property: JsonPropertyName("voice_id")] string VoiceId,
        [property: JsonPropertyName("response_format")] string ResponseFormat,
        [property: JsonPropertyName("stream")] bool Stream);
}
