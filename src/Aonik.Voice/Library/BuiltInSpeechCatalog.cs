using Aonik.SharedKernel.Abstractions.Ai.Speech;

namespace Aonik.Voice.Library;

/// <summary>
/// In-code archetype catalog. Built-ins ship with the binary — no DB seeding, no migration.
/// Tenants click "Clone" in the admin UI to get a tenant-owned, editable copy. The stable
/// <c>built-in:&lt;name&gt;</c> id format means active-recipe pointers to a built-in survive
/// across deploys.
///
/// <para>
/// This list is the source of truth for the v1.1 archetype set. Adding a new vendor / preset
/// requires (a) a new derived <see cref="SpeechProviderConfig"/> in SharedKernel, (b) a new
/// entry here, (c) a per-vendor form schema entry in the
/// <c>SpeechVendorsCatalogEndpoint</c>'s response.
/// </para>
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Built-in archetypes".
/// </para>
/// </summary>
internal sealed class BuiltInSpeechCatalog : IBuiltInSpeechCatalog
{
    /// <summary>UTC timestamp every built-in carries as its CreatedAt/UpdatedAt. Stable so
    /// snapshots are deterministic in tests.</summary>
    private static readonly DateTimeOffset BuiltInEpoch = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    public static readonly IReadOnlyList<SpeechProvider> Providers = new[]
    {
        // ── STT ────────────────────────────────────────────────────────────────────────
        Make(
            id: "built-in:openai-whisper-default",
            displayName: "OpenAI Whisper",
            type: SpeechProviderType.Stt,
            vendor: "openai",
            config: new OpenAIWhisperConfig(Model: "whisper-1", Language: null)),

        Make(
            id: "built-in:azure-stt-en-us-eastus",
            displayName: "Azure Speech (en-US, eastus)",
            type: SpeechProviderType.Stt,
            vendor: "azure",
            config: new AzureSttConfig(Region: "eastus", Language: "en-US")),

        // ── TTS ────────────────────────────────────────────────────────────────────────
        Make(
            id: "built-in:openai-tts-alloy",
            displayName: "OpenAI TTS · alloy",
            type: SpeechProviderType.Tts,
            vendor: "openai",
            config: new OpenAITtsConfig(VoiceId: "alloy", ModelId: "tts-1")),

        Make(
            id: "built-in:openai-tts-onyx-hd",
            displayName: "OpenAI TTS HD · onyx",
            type: SpeechProviderType.Tts,
            vendor: "openai",
            config: new OpenAITtsConfig(VoiceId: "onyx", ModelId: "tts-1-hd")),

        Make(
            id: "built-in:azure-tts-jenny-eastus",
            displayName: "Azure Jenny (en-US, eastus)",
            type: SpeechProviderType.Tts,
            vendor: "azure",
            config: new AzureTtsConfig(Region: "eastus", VoiceId: "en-US-JennyNeural")),

        Make(
            id: "built-in:elevenlabs-rachel",
            displayName: "ElevenLabs · Rachel",
            type: SpeechProviderType.Tts,
            vendor: "elevenlabs",
            config: new ElevenLabsTtsConfig(
                VoiceId: "21m00Tcm4TlvDq8ikWAM", // Rachel — well-known preset
                ModelId: "eleven_multilingual_v2",
                Stability: null,
                SimilarityBoost: null,
                OptimizeStreamingLatency: null)),

        // ── Composite ─────────────────────────────────────────────────────────────────
        Make(
            id: "built-in:openai-realtime",
            displayName: "OpenAI Realtime",
            type: SpeechProviderType.Composite,
            vendor: "openai-realtime",
            config: new OpenAIRealtimeCompositeConfig(
                Voice: "alloy",
                Model: "gpt-realtime-mini",
                InstructionsAddendum: null)),

        Make(
            id: "built-in:azure-voice-live-uksouth",
            displayName: "Azure Voice Live (uksouth)",
            type: SpeechProviderType.Composite,
            vendor: "azure-voice-live",
            config: new AzureVoiceLiveCompositeConfig(
                Region: "uksouth",
                Endpoint: "wss://uksouth.tts.speech.microsoft.com/cognitiveservices/voicelive",
                Voice: "alloy",
                Model: "gpt-realtime-mini",
                InstructionsAddendum: null)),
    };

    public IReadOnlyList<SpeechProvider> AllProviders => Providers;

    public SpeechProvider? FindProvider(string builtInId)
        => Providers.FirstOrDefault(p => string.Equals(p.Id, builtInId, StringComparison.Ordinal));

    private static SpeechProvider Make(
        string id,
        string displayName,
        SpeechProviderType type,
        string vendor,
        SpeechProviderConfig config)
    {
        if (!id.StartsWith(SpeechLibraryConstants.BuiltInIdPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Built-in id '{id}' must start with the reserved prefix '{SpeechLibraryConstants.BuiltInIdPrefix}'.");
        }

        return new SpeechProvider(
            Id: id,
            DisplayName: displayName,
            Type: type,
            Vendor: vendor,
            Config: config,
            Status: SpeechProviderStatus.Active,
            IsBuiltIn: true,
            Version: 1,
            CreatedAt: BuiltInEpoch,
            UpdatedAt: BuiltInEpoch,
            CreatedByUserId: null,
            LastUpdatedByUserId: null);
    }
}
