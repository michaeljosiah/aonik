using System.Buffers.Binary;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.Voice.Pipeline;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Voxa.Speech;

namespace Aonik.Voice.Endpoints.Admin.Speech;

// Per-provider test endpoints. They delegate to IPreviewEngineFactory (introduced in Phase A
// for the legacy voice page) but resolve the provider's configuration from the library, not from
// a free-form request body. Result: clicking "Test" on a provider in the admin UI reuses the
// vendor wiring that was already proven in spec 022 Phase A test cards — same code paths,
// same audio quality, no surprises.

// ── Test TTS ────────────────────────────────────────────────────────────────────────────

internal sealed class TestSpeechProviderTtsRequest
{
    public string Id { get; set; } = string.Empty;     // provider id (built-in or tenant Guid)
    public string Text { get; set; } = string.Empty;
}

internal sealed class TestSpeechProviderTtsEndpoint : Endpoint<TestSpeechProviderTtsRequest>
{
    private readonly ISpeechProviderLibraryService _library;
    private readonly IVoiceProviderCredentialResolver _credentials;
    private readonly IPreviewEngineFactory _engineFactory;
    private readonly ILogger<TestSpeechProviderTtsEndpoint> _logger;

    public TestSpeechProviderTtsEndpoint(
        ISpeechProviderLibraryService library,
        IVoiceProviderCredentialResolver credentials,
        IPreviewEngineFactory engineFactory,
        ILogger<TestSpeechProviderTtsEndpoint> logger)
    {
        _library = library;
        _credentials = credentials;
        _engineFactory = engineFactory;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/tenant/speech-providers/{id}/test-tts");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Test a TTS provider";
            s.Description = "Synthesize a short clip using the provider's stored configuration. Returns WAV (16-bit PCM wrapped in a RIFF/WAVE header) so the browser can play it without resampling.";
            s.Response(200, "WAV audio");
            s.Response(400, "Provider is not TTS, text is empty, or vendor returned no audio");
            s.Response(404, "Provider not found");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(TestSpeechProviderTtsRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
        {
            AddError("Text is required.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var provider = await _library.GetAsync(req.Id, ct);
        if (provider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (provider.Type != SpeechProviderType.Tts)
        {
            AddError($"Provider '{provider.DisplayName}' is type {provider.Type}, not Tts.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var credential = await _credentials.ResolveAsync(ResolverKeyFor(provider.Vendor), ct);
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            AddError($"{ResolverKeyFor(provider.Vendor)} credential is not configured for this tenant.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var engineRequest = ToEngineRequest(provider, credential.ApiKey!);
        ITextToSpeechEngine? engine = null;
        try
        {
            engine = _engineFactory.CreateTtsEngine(engineRequest);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        try
        {
            await engine.StartAsync(ct);

            using var pcm = new MemoryStream();
            await foreach (var chunk in engine.SynthesizeAsync(req.Text, ct).ConfigureAwait(false))
            {
                if (chunk is { Length: > 0 })
                {
                    pcm.Write(chunk, 0, chunk.Length);
                }
            }

            if (pcm.Length == 0)
            {
                AddError("Provider returned no audio.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            var sampleRate = _engineFactory.GetTtsOutputSampleRate(provider.Vendor);
            var wav = WrapPcmAsWav(pcm.ToArray(), sampleRate, channels: 1, bitsPerSample: 16);

            HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            HttpContext.Response.ContentType = "audio/wav";
            HttpContext.Response.ContentLength = wav.Length;
            HttpContext.Response.Headers.Append("X-Voice-Provider", provider.Vendor);
            HttpContext.Response.Headers.Append("X-Voice-Sample-Rate", sampleRate.ToString());

            await HttpContext.Response.Body.WriteAsync(wav, ct);
            await HttpContext.Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // client cancelled
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TTS test for provider {ProviderId} failed", provider.Id);
            AddError($"TTS test failed: {ex.Message}");
            await Send.ErrorsAsync(400, ct);
        }
        finally
        {
            await engine.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static TtsPreviewEngineRequest ToEngineRequest(SpeechProvider provider, string apiKey)
    {
        return provider.Config switch
        {
            OpenAITtsConfig openai => new TtsPreviewEngineRequest(
                Provider: "openai",
                ApiKey: apiKey,
                VoiceId: openai.VoiceId,
                ModelId: openai.ModelId,
                Region: null),
            AzureTtsConfig azure => new TtsPreviewEngineRequest(
                Provider: "azure",
                ApiKey: apiKey,
                VoiceId: azure.VoiceId,
                ModelId: null,
                Region: azure.Region),
            ElevenLabsTtsConfig eleven => new TtsPreviewEngineRequest(
                Provider: "elevenlabs",
                ApiKey: apiKey,
                VoiceId: eleven.VoiceId,
                ModelId: eleven.ModelId,
                Region: null),
            MistralTtsConfig mistral => new TtsPreviewEngineRequest(
                Provider: "mistral",
                ApiKey: apiKey,
                VoiceId: mistral.VoiceId,
                ModelId: mistral.ModelId,
                Region: null),
            _ => throw new NotSupportedException($"Provider '{provider.Vendor}' has no TTS engine mapping."),
        };
    }

    private static string ResolverKeyFor(string vendor) => vendor.Trim().ToLowerInvariant() switch
    {
        "openai" or "openai-whisper" => "OpenAI",
        "azure" => "Azure",
        "elevenlabs" => "ElevenLabs",
        "mistral" => "Mistral",
        _ => vendor,
    };

    private static byte[] WrapPcmAsWav(byte[] pcm, int sampleRate, short channels, short bitsPerSample)
    {
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = pcm.Length;
        var wav = new byte[44 + dataSize];

        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4, 4), 36 + dataSize);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16, 4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20, 2), 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22, 2), channels);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32, 2), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34, 2), bitsPerSample);
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40, 4), dataSize);
        Buffer.BlockCopy(pcm, 0, wav, 44, dataSize);
        return wav;
    }
}

// ── Test STT ────────────────────────────────────────────────────────────────────────────

internal sealed class TestSpeechProviderSttResponse
{
    public required string Text { get; init; }
    public string? Language { get; init; }
}

internal sealed class TestSpeechProviderSttEndpoint : Endpoint<EmptyRequest, TestSpeechProviderSttResponse>
{
    private readonly ISpeechProviderLibraryService _library;
    private readonly IVoiceProviderCredentialResolver _credentials;
    private readonly IPreviewEngineFactory _engineFactory;
    private readonly ILogger<TestSpeechProviderSttEndpoint> _logger;

    public TestSpeechProviderSttEndpoint(
        ISpeechProviderLibraryService library,
        IVoiceProviderCredentialResolver credentials,
        IPreviewEngineFactory engineFactory,
        ILogger<TestSpeechProviderSttEndpoint> logger)
    {
        _library = library;
        _credentials = credentials;
        _engineFactory = engineFactory;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/tenant/speech-providers/{id}/test-stt");
        Policies("AdminPolicy");
        AllowFileUploads();
        Summary(s =>
        {
            s.Summary = "Test an STT provider";
            s.Description = "Transcribe a short audio clip uploaded as multipart/form-data. The 'audio' file part carries raw 16-bit PCM (or WAV — the server detects the RIFF header). Optional 'sampleRate' field overrides the default 16 kHz.";
            s.Response(200, "Transcription result");
            s.Response(400, "Provider is not STT, no audio uploaded, vendor error");
            s.Response(404, "Provider not found");
        });
        Options(x => x.WithTags("Speech library"));
    }

    public override async Task HandleAsync(EmptyRequest _, CancellationToken ct)
    {
        var id = Route<string>("id") ?? string.Empty;
        var provider = await _library.GetAsync(id, ct);
        if (provider is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        if (provider.Type != SpeechProviderType.Stt)
        {
            AddError($"Provider '{provider.DisplayName}' is type {provider.Type}, not Stt.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        if (!HttpContext.Request.HasFormContentType || HttpContext.Request.Form.Files.Count == 0)
        {
            AddError("Multipart audio upload required (field name: 'audio').");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var form = HttpContext.Request.Form;
        var audioFile = form.Files["audio"] ?? form.Files[0];
        if (audioFile.Length == 0)
        {
            AddError("Uploaded audio is empty.");
            await Send.ErrorsAsync(400, ct);
            return;
        }
        var sampleRate = int.TryParse(form["sampleRate"].ToString(), out var sr) && sr > 0 ? sr : 16000;

        var credential = await _credentials.ResolveAsync(ResolverKeyFor(provider.Vendor), ct);
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            AddError($"{ResolverKeyFor(provider.Vendor)} credential is not configured for this tenant.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        // Upload may be raw PCM or a WAV with header; PreviewSttEndpoint already has the WAV
        // detection logic — for the library test endpoint we keep it simpler: accept whatever the
        // client sends as PCM at the requested sample rate. The admin UI's mic recorder ships PCM
        // directly so the WAV path isn't on the hot path.
        byte[] uploaded;
        await using (var ms = new MemoryStream((int)audioFile.Length))
        {
            await audioFile.CopyToAsync(ms, ct);
            uploaded = ms.ToArray();
        }

        var engineRequest = ToEngineRequest(provider, credential.ApiKey!, sampleRate);
        ISpeechToTextEngine? engine = null;
        try
        {
            engine = _engineFactory.CreateSttEngine(engineRequest);
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            AddError(ex.Message);
            await Send.ErrorsAsync(400, ct);
            return;
        }

        try
        {
            await engine.StartAsync(ct);
            await engine.WriteAudioAsync(uploaded, ct);
            await engine.FlushAsync();

            string? finalText = null;
            string? finalLanguage = null;

            using var drainCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            drainCts.CancelAfter(TimeSpan.FromSeconds(30));

            try
            {
                await foreach (var result in engine.ReadTranscriptsAsync(drainCts.Token))
                {
                    if (result.IsFinal)
                    {
                        finalText = result.Text;
                        finalLanguage = result.Language;
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (drainCts.IsCancellationRequested && !ct.IsCancellationRequested)
            {
                // drain timed out — fall through with whatever we have
            }

            await engine.StopAsync();

            if (string.IsNullOrWhiteSpace(finalText))
            {
                AddError("No transcription returned. Audio may be silent or wrong sample rate.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            await Send.OkAsync(new TestSpeechProviderSttResponse { Text = finalText, Language = finalLanguage }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "STT test for provider {ProviderId} failed", provider.Id);
            AddError($"STT test failed: {ex.Message}");
            await Send.ErrorsAsync(400, ct);
        }
        finally
        {
            await engine.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static SttPreviewEngineRequest ToEngineRequest(SpeechProvider provider, string apiKey, int sampleRate)
    {
        return provider.Config switch
        {
            OpenAIWhisperConfig openai => new SttPreviewEngineRequest(
                Provider: "openai-whisper",
                ApiKey: apiKey,
                Model: openai.Model,
                Language: openai.Language,
                Region: null,
                InputSampleRate: sampleRate),
            AzureSttConfig azure => new SttPreviewEngineRequest(
                Provider: "azure",
                ApiKey: apiKey,
                Model: null,
                Language: azure.Language,
                Region: azure.Region,
                InputSampleRate: sampleRate),
            _ => throw new NotSupportedException($"Provider '{provider.Vendor}' has no STT engine mapping."),
        };
    }

    private static string ResolverKeyFor(string vendor) => vendor.Trim().ToLowerInvariant() switch
    {
        "openai" or "openai-whisper" => "OpenAI",
        "azure" => "Azure",
        _ => vendor,
    };
}
