namespace Aonik.SharedKernel.Abstractions.Ai;

/// <summary>
/// Tenant-scoped voice provider configuration. Persisted as one JSON payload
/// under the <c>Platform.Voice.TenantProfile</c> setting key. v1 accepts only
/// <see cref="VoiceProviderKind.Chained"/>; composite kinds are reserved for
/// v1.1 (see <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 7).
/// </summary>
public sealed record VoiceProviderConfiguration(
    bool Enabled,
    VoiceProviderKind Kind,
    string? RecipeId,
    ChainedVoiceConfiguration? Chained)
{
    /// <summary>The "voice not configured for this tenant" default.</summary>
    public static VoiceProviderConfiguration Disabled { get; } =
        new(Enabled: false, Kind: VoiceProviderKind.Chained, RecipeId: null, Chained: null);
}

/// <summary>
/// Discriminator for the voice provider shape. v1 only validates
/// <see cref="Chained"/>; <see cref="VoiceLive"/>, <see cref="OpenAiRealtime"/>,
/// and <see cref="AzureOpenAiRealtime"/> are reserved values that
/// <c>VoiceProviderConfigurationValidator</c> will reject in v1 with a clear
/// "deferred to v1.1" error.
/// </summary>
public enum VoiceProviderKind
{
    Chained,
    VoiceLive,
    OpenAiRealtime,
    AzureOpenAiRealtime,
}

/// <summary>
/// Chained-pipeline configuration: separate STT, LLM (the AONIK agent), and
/// TTS components.
/// </summary>
public sealed record ChainedVoiceConfiguration(
    SttSettings Stt,
    TtsSettings Tts,
    VadSettings Vad,
    bool TranscriptionFilter,
    bool SentenceAggregator);

public sealed record SttSettings(string Vendor, string? Model);

public sealed record TtsSettings(string Vendor, string? VoiceId, string? ModelId);

public sealed record VadSettings(string Kind, int? StopMs);
