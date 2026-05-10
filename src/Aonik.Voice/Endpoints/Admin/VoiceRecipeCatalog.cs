using Aonik.SharedKernel.Abstractions.Ai;

namespace Aonik.Voice.Endpoints.Admin;

/// <summary>
/// Hand-curated list of v1 voice recipes — surfaced through
/// <c>GET /tenant/settings/voice/recipes</c> for the admin UI's recipe picker.
/// Mirrors the recipes table in
/// <c>docs/specifications/022.aonik-voice-realtime.md</c>.
///
/// <para>
/// v1 actually wires only <c>cost-chained-openai</c>; the other recipes are
/// returned with <c>Implemented = false</c> so the front-end can show them as
/// "coming soon" without hiding the eventual product surface.
/// </para>
/// </summary>
internal static class VoiceRecipeCatalog
{
    public static IReadOnlyList<VoiceRecipeResponse> All { get; } = new[]
    {
        new VoiceRecipeResponse(
            Id: "cost-chained-openai",
            Name: "Cost chained — OpenAI",
            Description: "Whisper STT → gpt-4o-mini → OpenAI TTS (alloy). Lowest cost, ~2 s p95 first audio.",
            CostRanking: "$",
            LatencyTarget: "~2 s",
            Implemented: true,
            Settings: new VoiceProviderSettingsResponse(
                Enabled: true,
                Kind: "chained",
                RecipeId: "cost-chained-openai",
                Chained: new ChainedVoiceSettingsResponse(
                    Stt: new SttSettingsResponse("openai-whisper", "whisper-1"),
                    Tts: new TtsSettingsResponse("openai", "alloy", "tts-1"),
                    Vad: new VadSettingsResponse("energy", 800),
                    TranscriptionFilter: true,
                    SentenceAggregator: true))),

        new VoiceRecipeResponse(
            Id: "premium-voice-chained",
            Name: "Premium voice chained",
            Description: "Whisper STT → gpt-4o-mini → ElevenLabs (custom voice). Higher cost, premium voice.",
            CostRanking: "$$",
            LatencyTarget: "~2 s",
            Implemented: false,
            Settings: new VoiceProviderSettingsResponse(
                Enabled: false,
                Kind: "chained",
                RecipeId: "premium-voice-chained",
                Chained: new ChainedVoiceSettingsResponse(
                    Stt: new SttSettingsResponse("openai-whisper", "whisper-1"),
                    Tts: new TtsSettingsResponse("elevenlabs", null, "eleven_turbo_v2_5"),
                    Vad: new VadSettingsResponse("energy", 800),
                    TranscriptionFilter: true,
                    SentenceAggregator: true))),

        new VoiceRecipeResponse(
            Id: "azure-only-chained",
            Name: "Azure-only chained",
            Description: "Azure Speech STT → Azure OpenAI gpt-4o-mini → Azure Speech TTS.",
            CostRanking: "$",
            LatencyTarget: "~2 s",
            Implemented: false,
            Settings: new VoiceProviderSettingsResponse(
                Enabled: false,
                Kind: "chained",
                RecipeId: "azure-only-chained",
                Chained: new ChainedVoiceSettingsResponse(
                    Stt: new SttSettingsResponse("azure", null),
                    Tts: new TtsSettingsResponse("azure", null, null),
                    Vad: new VadSettingsResponse("energy", 800),
                    TranscriptionFilter: true,
                    SentenceAggregator: true))),

        new VoiceRecipeResponse(
            Id: "mixed-cost-optimized",
            Name: "Mixed cost-optimized",
            Description: "Azure Speech STT → gpt-4o-mini → Mistral Voxtral TTS.",
            CostRanking: "$",
            LatencyTarget: "~2 s",
            Implemented: false,
            Settings: new VoiceProviderSettingsResponse(
                Enabled: false,
                Kind: "chained",
                RecipeId: "mixed-cost-optimized",
                Chained: new ChainedVoiceSettingsResponse(
                    Stt: new SttSettingsResponse("azure", null),
                    Tts: new TtsSettingsResponse("mistral", null, null),
                    Vad: new VadSettingsResponse("energy", 800),
                    TranscriptionFilter: true,
                    SentenceAggregator: true))),

        // Premium chained recipe: same shape as cost-chained-openai but using gpt-4o + tts-1-hd
        // for higher fidelity. The pipeline factory already accepts any model id so this is wired
        // end-to-end in v1 — the only difference from cost-chained-openai is the chosen models.
        new VoiceRecipeResponse(
            Id: "premium-chained-openai",
            Name: "Premium chained — OpenAI",
            Description: "Whisper STT → gpt-4o → OpenAI TTS HD (onyx). Higher fidelity, ~3-4× the cost of cost-chained-openai.",
            CostRanking: "$$",
            LatencyTarget: "~2 s",
            Implemented: true,
            Settings: new VoiceProviderSettingsResponse(
                Enabled: true,
                Kind: "chained",
                RecipeId: "premium-chained-openai",
                Chained: new ChainedVoiceSettingsResponse(
                    Stt: new SttSettingsResponse("openai-whisper", "whisper-1"),
                    Tts: new TtsSettingsResponse("openai", "onyx", "tts-1-hd"),
                    Vad: new VadSettingsResponse("energy", 800),
                    TranscriptionFilter: true,
                    SentenceAggregator: true))),

        // OpenAI Realtime composite — STT+LLM+TTS+VAD via OpenAI's realtime API in a single
        // socket. Voxa already ships the AzureVoiceLiveProcessor / OpenAIRealtimeProcessor; the
        // pipeline factory needs a kind="openai-realtime" branch before this flips to Implemented.
        new VoiceRecipeResponse(
            Id: "openai-realtime",
            Name: "OpenAI Realtime",
            Description: "OpenAI's realtime API end-to-end (single socket, server-side VAD, sub-second turn-taking).",
            CostRanking: "$$$",
            LatencyTarget: "~1 s",
            Implemented: false,
            Settings: new VoiceProviderSettingsResponse(
                Enabled: false,
                Kind: "openai-realtime",
                RecipeId: "openai-realtime",
                Chained: null)),
    };

    /// <summary>Voices the front-end can pick from for a given provider.</summary>
    public static IReadOnlyList<VoiceOptionResponse> VoicesFor(string provider)
    {
        var normalized = (provider ?? "openai").Trim().ToLowerInvariant();
        return normalized switch
        {
            "openai" or "openai-whisper" => new[]
            {
                new VoiceOptionResponse("alloy", "Alloy", "Balanced, neutral voice."),
                new VoiceOptionResponse("echo", "Echo", "Warm, slightly lower."),
                new VoiceOptionResponse("fable", "Fable", "Expressive, narrative."),
                new VoiceOptionResponse("onyx", "Onyx", "Deep, authoritative."),
                new VoiceOptionResponse("nova", "Nova", "Bright, energetic."),
                new VoiceOptionResponse("shimmer", "Shimmer", "Soft, friendly."),
            },

            // Azure neural voices — small curated en-US set; the full catalog has hundreds.
            // Admins who need a region-specific voice can type the id directly in the API call;
            // the picker just needs sensible defaults.
            "azure" => new[]
            {
                new VoiceOptionResponse("en-US-JennyNeural", "Jenny (en-US)", "Warm, conversational neural voice."),
                new VoiceOptionResponse("en-US-AriaNeural", "Aria (en-US)", "Friendly, cheerful neural voice."),
                new VoiceOptionResponse("en-US-GuyNeural", "Guy (en-US)", "Deeper male neural voice."),
                new VoiceOptionResponse("en-GB-SoniaNeural", "Sonia (en-GB)", "British English female neural voice."),
                new VoiceOptionResponse("en-AU-NatashaNeural", "Natasha (en-AU)", "Australian English female neural voice."),
            },

            // ElevenLabs — preset library voices (well-known IDs, freely usable on the standard plan).
            "elevenlabs" => new[]
            {
                new VoiceOptionResponse("21m00Tcm4TlvDq8ikWAM", "Rachel", "Calm, narrative-friendly female voice."),
                new VoiceOptionResponse("AZnzlk1XvdvUeBnXmlld", "Domi", "Strong, confident female voice."),
                new VoiceOptionResponse("EXAVITQu4vr4xnSDxMaL", "Bella", "Soft, friendly female voice."),
                new VoiceOptionResponse("ErXwobaYiN019PkySvjV", "Antoni", "Well-rounded male voice."),
                new VoiceOptionResponse("VR6AewLTigWG4xSOukaG", "Arnold", "Crisp, authoritative male voice."),
            },

            // Mistral Voxtral-TTS — voice catalog is small and overlaps with OpenAI's preset names.
            "mistral" => new[]
            {
                new VoiceOptionResponse("alloy", "Alloy", "Balanced neutral voice."),
                new VoiceOptionResponse("echo", "Echo", "Warm, slightly lower."),
                new VoiceOptionResponse("nova", "Nova", "Bright, energetic."),
                new VoiceOptionResponse("shimmer", "Shimmer", "Soft, friendly."),
            },

            _ => Array.Empty<VoiceOptionResponse>(),
        };
    }
}
