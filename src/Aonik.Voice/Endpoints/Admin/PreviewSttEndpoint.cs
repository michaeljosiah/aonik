using System.Buffers.Binary;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.Voice.Pipeline;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Voxa.Speech;

namespace Aonik.Voice.Endpoints.Admin;

/// <summary>
/// Transcribes a short audio clip the admin UI captured from the user's mic so admins can validate
/// an STT credential / language / region before saving. Multipart upload — <c>audio</c> file +
/// metadata fields (<c>provider</c>, <c>model</c>, <c>language</c>, <c>region</c>, <c>sampleRate</c>).
///
/// <para>
/// Accepts both raw 16-bit PCM and WAV. WAV is detected by the <c>RIFF....WAVE</c> magic bytes
/// and the data chunk is extracted; PCM is fed straight through. The chosen STT engine receives
/// the audio in one buffered call (this is preview, not pipeline-streaming).
/// </para>
/// </summary>
internal sealed class PreviewSttEndpoint : Endpoint<EmptyRequest, SttPreviewResponse>
{
    private readonly IVoiceProviderCredentialResolver _credentialResolver;
    private readonly IPreviewEngineFactory _engineFactory;
    private readonly ILogger<PreviewSttEndpoint> _logger;

    public PreviewSttEndpoint(
        IVoiceProviderCredentialResolver credentialResolver,
        IPreviewEngineFactory engineFactory,
        ILogger<PreviewSttEndpoint> logger)
    {
        _credentialResolver = credentialResolver;
        _engineFactory = engineFactory;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/tenant/settings/voice/preview-stt");
        Policies("AdminPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Preview speech-to-text transcription";
            s.Description = "Transcribes the supplied audio clip using the requested STT provider so admins can verify credentials, model, and language before saving. Accepts raw 16-bit PCM or WAV.";
            s.Response(200, "Transcription returned");
            s.Response(400, "Invalid request, missing audio, missing credential, or provider error");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        if (!HttpContext.Request.HasFormContentType || HttpContext.Request.Form.Files.Count == 0)
        {
            AddError("Multipart audio upload required (field name: 'audio').");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var form = HttpContext.Request.Form;
        var provider = (form["provider"].ToString() ?? "openai-whisper").Trim();
        var model = NullIfEmpty(form["model"].ToString());
        var language = NullIfEmpty(form["language"].ToString());
        var region = NullIfEmpty(form["region"].ToString());
        var sampleRate = int.TryParse(form["sampleRate"].ToString(), out var sr) && sr > 0 ? sr : 16000;

        var resolverKey = ResolverKeyFor(provider);
        var credential = await _credentialResolver.ResolveAsync(resolverKey, ct);
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            AddError($"{resolverKey} STT credential is not configured for this tenant.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var audioFile = form.Files["audio"] ?? form.Files[0];
        if (audioFile.Length == 0)
        {
            AddError("Uploaded audio is empty.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Read whole file (preview clips are short).
        byte[] uploaded;
        await using (var ms = new MemoryStream((int)audioFile.Length))
        {
            await audioFile.CopyToAsync(ms, ct);
            uploaded = ms.ToArray();
        }

        // Detect WAV vs raw PCM. WAV is RIFF{4-byte size}WAVE.
        ReadOnlyMemory<byte> pcmBytes;
        try
        {
            pcmBytes = LooksLikeWav(uploaded)
                ? ExtractWavPcm(uploaded, out var detectedSampleRate)
                    .Tee(_ => sampleRate = detectedSampleRate)
                : uploaded;
        }
        catch (Exception ex)
        {
            AddError($"Failed to parse uploaded audio: {ex.Message}");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        ISpeechToTextEngine? engine = null;
        try
        {
            engine = _engineFactory.CreateSttEngine(new SttPreviewEngineRequest(
                Provider: provider,
                ApiKey: credential.ApiKey!,
                Model: model,
                Language: language,
                Region: region,
                InputSampleRate: sampleRate));
        }
        catch (NotSupportedException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
            return;
        }
        catch (ArgumentException ex)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        try
        {
            await engine.StartAsync(ct);
            await engine.WriteAudioAsync(pcmBytes, ct);
            await engine.FlushAsync();

            // Drain transcripts. Most engines (OpenAI Whisper, Azure batch) emit a single final
            // result for a one-shot upload like this; we collect any interim hypotheses too and
            // join them as a fallback if no final arrives within a short grace window.
            string? finalText = null;
            string? language2 = null;
            var interim = new List<string>();

            // Time-bound the drain — preview clips are short, the engine should respond in seconds.
            using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            drainCts.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                await foreach (var result in engine.ReadTranscriptsAsync(drainCts.Token).ConfigureAwait(false))
                {
                    if (result.IsFinal)
                    {
                        finalText = result.Text;
                        language2 = result.Language;
                        break;
                    }
                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        interim.Add(result.Text);
                    }
                }
            }
            catch (OperationCanceledException) when (drainCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // Drain timed out — fall through to interim concatenation if any.
            }

            await engine.StopAsync();

            var text = !string.IsNullOrWhiteSpace(finalText)
                ? finalText!
                : string.Join(' ', interim).Trim();

            if (string.IsNullOrWhiteSpace(text))
            {
                AddError("No transcription returned. Check the audio (silent? wrong sample rate?), credential, and language.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            await Send.OkAsync(new SttPreviewResponse(text, language2), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client cancelled.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "STT preview failed");
            AddError($"STT preview failed: {ex.Message}");
            await Send.ErrorsAsync(400, ct);
        }
        finally
        {
            await engine.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string? NullIfEmpty(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolverKeyFor(string provider) => provider.Trim().ToLowerInvariant() switch
    {
        "openai" or "openai-whisper" => "OpenAI",
        "azure" => "Azure",
        _ => provider,
    };

    private static bool LooksLikeWav(byte[] bytes)
        => bytes.Length >= 12
           && bytes[0] == 'R' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == 'F'
           && bytes[8] == 'W' && bytes[9] == 'A' && bytes[10] == 'V' && bytes[11] == 'E';

    /// <summary>
    /// Extract the <c>data</c> sub-chunk PCM bytes from a WAV file, returning the sample rate so
    /// the engine can be told what to expect. Tolerates fmt-chunk extensions by chunk-walking.
    /// </summary>
    private static byte[] ExtractWavPcm(byte[] wav, out int sampleRate)
    {
        // Skip RIFF header (12 bytes), then walk chunks until "data".
        var span = wav.AsSpan();
        sampleRate = 0;
        var i = 12;

        while (i + 8 <= span.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(span.Slice(i, 4));
            var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(i + 4, 4));
            var chunkStart = i + 8;

            if (chunkId == "fmt " && chunkSize >= 16)
            {
                sampleRate = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(chunkStart + 4, 4));
            }
            else if (chunkId == "data")
            {
                var pcm = new byte[chunkSize];
                Buffer.BlockCopy(wav, chunkStart, pcm, 0, chunkSize);
                if (sampleRate == 0) sampleRate = 16000; // last-ditch default
                return pcm;
            }

            // Chunks are word-aligned.
            i = chunkStart + chunkSize + (chunkSize % 2);
        }

        throw new InvalidDataException("WAV file has no 'data' chunk.");
    }
}

internal static class WavExtensions
{
    /// <summary>Inline side-effect on a value — like Kotlin's <c>also</c>.</summary>
    public static T Tee<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
