using System.Buffers.Binary;
using Aonik.SharedKernel.Abstractions.Ai;
using Aonik.Voice.Pipeline;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Voxa.Speech;

namespace Aonik.Voice.Endpoints.Admin;

/// <summary>
/// Synthesizes a short audio sample of the configured voice so admins can hear it before saving.
/// Multi-provider via <see cref="IPreviewEngineFactory"/> — OpenAI, Azure, ElevenLabs, Mistral.
///
/// <para>
/// Returns the synthesized PCM wrapped in a minimal WAV header so the browser's <c>&lt;audio&gt;</c>
/// element can play it back without resampling. Sample rate / channel count come from the engine's
/// declared output format.
/// </para>
/// </summary>
internal sealed class PreviewVoiceEndpoint : Endpoint<VoicePreviewRequest>
{
    private readonly IVoiceProviderCredentialResolver _credentialResolver;
    private readonly IPreviewEngineFactory _engineFactory;
    private readonly ILogger<PreviewVoiceEndpoint> _logger;

    public PreviewVoiceEndpoint(
        IVoiceProviderCredentialResolver credentialResolver,
        IPreviewEngineFactory engineFactory,
        ILogger<PreviewVoiceEndpoint> logger)
    {
        _credentialResolver = credentialResolver;
        _engineFactory = engineFactory;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/tenant/settings/voice/preview");
        Policies("AdminPolicy");
        Summary(s =>
        {
            s.Summary = "Preview voice synthesis";
            s.Description = "Synthesizes a short audio sample using the supplied voice + provider so admins can hear it before saving. Supports OpenAI, Azure, ElevenLabs, and Mistral.";
            s.Response(200, "WAV audio stream returned");
            s.Response(400, "Invalid request, unknown provider, missing credential, or provider error");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Settings"));
    }

    public override async Task HandleAsync(VoicePreviewRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Text))
        {
            AddError("Text must be supplied for the voice preview.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        var provider = (req.Provider ?? "openai").Trim();
        var resolverKey = ResolverKeyFor(provider);

        var credential = await _credentialResolver.ResolveAsync(resolverKey, ct);
        if (!credential.HasCredential || string.IsNullOrWhiteSpace(credential.ApiKey))
        {
            AddError($"{resolverKey} voice credential is not configured for this tenant.");
            await Send.ErrorsAsync(400, ct);
            return;
        }

        ITextToSpeechEngine? engine = null;
        try
        {
            engine = _engineFactory.CreateTtsEngine(new TtsPreviewEngineRequest(
                Provider: provider,
                ApiKey: credential.ApiKey!,
                VoiceId: req.VoiceId,
                ModelId: req.ModelId,
                Region: req.Region));
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

            // Buffer the entire synthesis in memory — preview clips are short (1-2 sentences, < 10 s
            // of audio at 24 kHz mono ~480 KB) and we want to set Content-Length / write the WAV
            // header before any bytes flush.
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
                AddError("Provider returned no audio. Check credential, voice id, and model id.");
                await Send.ErrorsAsync(400, ct);
                return;
            }

            var sampleRate = _engineFactory.GetTtsOutputSampleRate(provider);
            var pcmBytes = pcm.ToArray();
            var wavBytes = WrapPcmAsWav(pcmBytes, sampleRate, channels: 1, bitsPerSample: 16);

            HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            HttpContext.Response.ContentType = "audio/wav";
            HttpContext.Response.ContentLength = wavBytes.Length;
            HttpContext.Response.Headers.Append("X-Voice-Provider", provider);
            HttpContext.Response.Headers.Append("X-Voice-Id", req.VoiceId ?? string.Empty);
            HttpContext.Response.Headers.Append("X-Voice-Sample-Rate", sampleRate.ToString());

            await HttpContext.Response.Body.WriteAsync(wavBytes, ct);
            await HttpContext.Response.Body.FlushAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client cancelled — nothing to report.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Voice preview synthesis failed");
            AddError($"Voice preview synthesis failed: {ex.Message}");
            await Send.ErrorsAsync(400, ct);
        }
        finally
        {
            await engine.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Map our provider name to the credential resolver's provider key. The resolver historically
    /// stores per-vendor keys under conventional names (e.g. "OpenAI", "Azure", "ElevenLabs"),
    /// while our admin UI uses lowercase shortcodes.
    /// </summary>
    private static string ResolverKeyFor(string provider) => provider.Trim().ToLowerInvariant() switch
    {
        "openai" or "openai-whisper" => "OpenAI",
        "azure" => "Azure",
        "elevenlabs" => "ElevenLabs",
        "mistral" => "Mistral",
        _ => provider,
    };

    /// <summary>
    /// Build a minimal RIFF/WAVE header around raw PCM. 44-byte canonical layout — sufficient for
    /// browser <c>&lt;audio&gt;</c> playback. Source spec:
    /// <see href="http://soundfile.sapp.org/doc/WaveFormat/"/>.
    /// </summary>
    private static byte[] WrapPcmAsWav(byte[] pcm, int sampleRate, short channels, short bitsPerSample)
    {
        var byteRate = sampleRate * channels * bitsPerSample / 8;
        var blockAlign = (short)(channels * bitsPerSample / 8);
        var dataSize = pcm.Length;
        var wav = new byte[44 + dataSize];

        // RIFF header
        System.Text.Encoding.ASCII.GetBytes("RIFF").CopyTo(wav, 0);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(4, 4), 36 + dataSize);
        System.Text.Encoding.ASCII.GetBytes("WAVE").CopyTo(wav, 8);

        // fmt sub-chunk
        System.Text.Encoding.ASCII.GetBytes("fmt ").CopyTo(wav, 12);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(16, 4), 16); // PCM fmt size
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20, 2), 1);  // PCM format
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(22, 2), channels);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(24, 4), sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(28, 4), byteRate);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(32, 2), blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(34, 2), bitsPerSample);

        // data sub-chunk
        System.Text.Encoding.ASCII.GetBytes("data").CopyTo(wav, 36);
        BinaryPrimitives.WriteInt32LittleEndian(wav.AsSpan(40, 4), dataSize);
        Buffer.BlockCopy(pcm, 0, wav, 44, dataSize);

        return wav;
    }
}
