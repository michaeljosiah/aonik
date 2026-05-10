using Voxa.Speech;
using Voxa.Speech.Azure;
using Voxa.Speech.ElevenLabs;
using Voxa.Speech.Mistral;
using Voxa.Speech.OpenAI;

namespace Aonik.Voice.Pipeline;

/// <summary>
/// Inputs for a one-off TTS preview synthesis. Mirrors the shape of the admin UI's TTS test card —
/// host supplies the provider name plus any provider-specific fields (voice, model, region, …) and
/// gets back a Voxa <see cref="ITextToSpeechEngine"/> ready to <c>SynthesizeAsync</c>. Credentials
/// are resolved separately by the caller via <c>IVoiceProviderCredentialResolver</c>.
/// </summary>
public sealed record TtsPreviewEngineRequest(
    string Provider,
    string ApiKey,
    string? VoiceId,
    string? ModelId,
    string? Region);

/// <summary>Inputs for a one-off STT preview transcription.</summary>
public sealed record SttPreviewEngineRequest(
    string Provider,
    string ApiKey,
    string? Model,
    string? Language,
    string? Region,
    int InputSampleRate);

/// <summary>
/// Builds Voxa speech engines on demand for the admin "Test STT/TTS" surface. Decouples the
/// preview endpoints from the per-vendor wiring details so adding a new vendor is a one-line
/// change here, not three changes scattered across endpoints.
///
/// <para>
/// Returns a fresh engine per call; callers are responsible for disposing it
/// (<c>await using</c>) since these are one-shot preview engines, not pipeline-scoped.
/// </para>
/// </summary>
public interface IPreviewEngineFactory
{
    ITextToSpeechEngine CreateTtsEngine(TtsPreviewEngineRequest request);
    ISpeechToTextEngine CreateSttEngine(SttPreviewEngineRequest request);

    /// <summary>
    /// Output sample rate the engine will produce. Useful so callers can wrap raw PCM in a WAV
    /// header without inspecting per-vendor defaults.
    /// </summary>
    int GetTtsOutputSampleRate(string provider);
}

internal sealed class PreviewEngineFactory : IPreviewEngineFactory
{
    public ITextToSpeechEngine CreateTtsEngine(TtsPreviewEngineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = NormalizeProvider(request.Provider);

        return provider switch
        {
            "openai" => new OpenAITextToSpeechEngine(new OpenAISpeechOptions
            {
                ApiKey = request.ApiKey,
                TtsVoice = string.IsNullOrWhiteSpace(request.VoiceId) ? "alloy" : request.VoiceId,
                TtsModel = string.IsNullOrWhiteSpace(request.ModelId) ? "tts-1" : request.ModelId,
            }),

            "azure" => new AzureTextToSpeechEngine(new AzureSpeechOptions
            {
                SubscriptionKey = request.ApiKey,
                Region = RequireRegion(request.Region, provider),
                Voice = string.IsNullOrWhiteSpace(request.VoiceId) ? "en-US-JennyNeural" : request.VoiceId,
            }),

            "elevenlabs" => new ElevenLabsTextToSpeechEngine(new ElevenLabsOptions
            {
                ApiKey = request.ApiKey,
                VoiceId = string.IsNullOrWhiteSpace(request.VoiceId)
                    ? throw new ArgumentException("ElevenLabs preview requires a voice id.")
                    : request.VoiceId,
                ModelId = string.IsNullOrWhiteSpace(request.ModelId) ? "eleven_multilingual_v2" : request.ModelId,
            }),

            "mistral" => new MistralTextToSpeechEngine(new MistralSpeechOptions
            {
                ApiKey = request.ApiKey,
                Voice = string.IsNullOrWhiteSpace(request.VoiceId) ? "alloy" : request.VoiceId,
                // Mistral's actual TTS model id — the production MistralTextToSpeechProvider
                // hardcodes the same value. The old "voxtral-tts" placeholder was wrong and
                // caused 400s from Mistral's /v1/audio/speech endpoint, so we also rewrite
                // it on the fly here to spare admins editing every existing provider row.
                Model = ResolveMistralModel(request.ModelId),
            }),

            _ => throw new NotSupportedException(
                $"TTS provider '{request.Provider}' is not supported. Use 'openai', 'azure', 'elevenlabs', or 'mistral'."),
        };
    }

    public ISpeechToTextEngine CreateSttEngine(SttPreviewEngineRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var provider = NormalizeProvider(request.Provider);

        return provider switch
        {
            "openai" or "openai-whisper" => new OpenAIWhisperEngine(new OpenAISpeechOptions
            {
                ApiKey = request.ApiKey,
                SttModel = string.IsNullOrWhiteSpace(request.Model) ? "whisper-1" : request.Model,
                SttLanguage = string.IsNullOrWhiteSpace(request.Language) ? null : request.Language,
                InputSampleRate = request.InputSampleRate > 0 ? request.InputSampleRate : 16000,
            }),

            "azure" => new AzureSpeechToTextEngine(new AzureSpeechOptions
            {
                SubscriptionKey = request.ApiKey,
                Region = RequireRegion(request.Region, provider),
                RecognitionLanguage = string.IsNullOrWhiteSpace(request.Language) ? "en-US" : request.Language,
                InputSampleRate = request.InputSampleRate > 0 ? request.InputSampleRate : 16000,
            }),

            _ => throw new NotSupportedException(
                $"STT provider '{request.Provider}' is not supported. Use 'openai' / 'openai-whisper' or 'azure'."),
        };
    }

    public int GetTtsOutputSampleRate(string provider)
    {
        // Voxa engine defaults — keep in sync with the *Options records above. The preview endpoint
        // wraps the raw PCM in a WAV header that needs the correct sample rate so the browser can
        // play it back without resampling.
        return NormalizeProvider(provider) switch
        {
            "openai" => 24000,      // OpenAITextToSpeechEngine outputs 24 kHz PCM
            "azure" => 24000,       // AzureTextToSpeechEngine outputs 24 kHz PCM
            "elevenlabs" => 24000,  // ElevenLabsTextToSpeechEngine default
            "mistral" => 24000,     // MistralTextToSpeechEngine default
            _ => 24000,
        };
    }

    private static string NormalizeProvider(string provider)
        => (provider ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Bridge for the rename of Mistral's TTS model id from the early placeholder
    /// "voxtral-tts" (which was never a real Mistral model and 400s on /v1/audio/speech)
    /// to the production id "voxtral-mini-tts-2603". Existing SpeechProvider rows still
    /// carry the old value in their config; rewrite on the fly so admins don't have to
    /// edit every row by hand.
    /// </summary>
    private static string ResolveMistralModel(string? modelId)
    {
        if (string.IsNullOrWhiteSpace(modelId)) return "voxtral-mini-tts-2603";
        return modelId.Trim().Equals("voxtral-tts", StringComparison.OrdinalIgnoreCase)
            ? "voxtral-mini-tts-2603"
            : modelId;
    }

    private static string RequireRegion(string? region, string provider)
        => string.IsNullOrWhiteSpace(region)
            ? throw new ArgumentException($"Region is required for {provider} preview (e.g. 'eastus', 'westeurope').")
            : region;
}
