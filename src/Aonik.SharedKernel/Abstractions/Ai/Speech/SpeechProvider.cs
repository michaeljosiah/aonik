using System.Text.Json.Serialization;

namespace Aonik.SharedKernel.Abstractions.Ai.Speech;

/// <summary>
/// One configured speech provider in a tenant's library. Many can coexist for the same vendor —
/// e.g. "OpenAI TTS — alloy" and "OpenAI TTS HD — onyx" are two separate <see cref="SpeechProvider"/>
/// rows of vendor <c>openai</c> with different <see cref="Config"/>.
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Provider library".
/// </para>
/// </summary>
public sealed record SpeechProvider(
    /// <summary>Stable id. Built-in archetypes use <c>built-in:&lt;name&gt;</c>; tenant rows use Guid.</summary>
    string Id,
    string DisplayName,
    SpeechProviderType Type,
    /// <summary>
    /// Vendor shortcode — <c>openai</c>, <c>azure</c>, <c>elevenlabs</c>, <c>mistral</c>,
    /// <c>openai-realtime</c>, <c>azure-voice-live</c>. The set is fixed; new vendors require
    /// adding a derived <see cref="SpeechProviderConfig"/> + a server-side enum entry.
    /// </summary>
    string Vendor,
    SpeechProviderConfig Config,
    SpeechProviderStatus Status,
    /// <summary>True for archetypes that ship in code via <c>BuiltInSpeechCatalog</c>; false for tenant-owned.</summary>
    bool IsBuiltIn,
    /// <summary>Increments on every Update. Built-ins are always Version 1.</summary>
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CreatedByUserId,
    Guid? LastUpdatedByUserId);

/// <summary>Whether the provider is for transcription, synthesis, or end-to-end realtime.</summary>
public enum SpeechProviderType
{
    Stt,
    Tts,
    Composite,
}

/// <summary>Lifecycle state. Soft-delete keeps the row + audit trail; physical delete is operator-only.</summary>
public enum SpeechProviderStatus
{
    Active,
    Disabled,
    SoftDeleted,
}

/// <summary>
/// Polymorphic vendor-specific configuration. The discriminator on the wire is the <c>kind</c>
/// JSON property, which equals the <see cref="SpeechProvider.Vendor"/> shortcode for type-narrow
/// vendors (e.g. <c>openai-tts</c>) so the UI can render a per-vendor form without a separate
/// catalog lookup.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(OpenAIWhisperConfig), "openai-whisper")]
[JsonDerivedType(typeof(AzureSttConfig), "azure-stt")]
[JsonDerivedType(typeof(OpenAITtsConfig), "openai-tts")]
[JsonDerivedType(typeof(AzureTtsConfig), "azure-tts")]
[JsonDerivedType(typeof(ElevenLabsTtsConfig), "elevenlabs-tts")]
[JsonDerivedType(typeof(MistralTtsConfig), "mistral-tts")]
[JsonDerivedType(typeof(OpenAIRealtimeCompositeConfig), "openai-realtime")]
[JsonDerivedType(typeof(AzureVoiceLiveCompositeConfig), "azure-voice-live")]
public abstract record SpeechProviderConfig;

// ── STT configs ─────────────────────────────────────────────────────────────────────────────

/// <summary>OpenAI Whisper STT — used by <c>Voxa.Speech.OpenAI.OpenAIWhisperEngine</c>.</summary>
public sealed record OpenAIWhisperConfig(string? Model, string? Language) : SpeechProviderConfig;

/// <summary>Azure Speech STT — used by <c>Voxa.Speech.Azure.AzureSpeechToTextEngine</c>.</summary>
public sealed record AzureSttConfig(string Region, string? Language) : SpeechProviderConfig;

// ── TTS configs ─────────────────────────────────────────────────────────────────────────────

/// <summary>OpenAI TTS — used by <c>Voxa.Speech.OpenAI.OpenAITextToSpeechEngine</c>.</summary>
public sealed record OpenAITtsConfig(string VoiceId, string? ModelId) : SpeechProviderConfig;

/// <summary>Azure Speech TTS — used by <c>Voxa.Speech.Azure.AzureTextToSpeechEngine</c>.</summary>
public sealed record AzureTtsConfig(string Region, string VoiceId) : SpeechProviderConfig;

/// <summary>ElevenLabs TTS — used by <c>Voxa.Speech.ElevenLabs.ElevenLabsTextToSpeechEngine</c>.</summary>
public sealed record ElevenLabsTtsConfig(
    string VoiceId,
    string? ModelId,
    double? Stability,
    double? SimilarityBoost,
    int? OptimizeStreamingLatency) : SpeechProviderConfig;

/// <summary>Mistral Voxtral TTS — used by <c>Voxa.Speech.Mistral.MistralTextToSpeechEngine</c>.</summary>
public sealed record MistralTtsConfig(string VoiceId, string? ModelId) : SpeechProviderConfig;

// ── Composite configs ───────────────────────────────────────────────────────────────────────

/// <summary>
/// OpenAI Realtime end-to-end composite — used by <c>Voxa.Services.OpenAIRealtime.OpenAIRealtimeProcessor</c>.
/// Realtime providers carry their own voice catalog distinct from chained TTS.
/// </summary>
public sealed record OpenAIRealtimeCompositeConfig(
    string Voice,
    string? Model,
    string? InstructionsAddendum) : SpeechProviderConfig;

/// <summary>
/// Azure Voice Live end-to-end composite — used by <c>Voxa.Services.AzureVoiceLive.AzureVoiceLiveProcessor</c>.
/// Endpoint is required because Voice Live is region-pinned.
/// </summary>
public sealed record AzureVoiceLiveCompositeConfig(
    string Region,
    string Endpoint,
    string Voice,
    string? Model,
    string? InstructionsAddendum) : SpeechProviderConfig;

/// <summary>One snapshot of a <see cref="SpeechProvider"/> at a prior version. Read-only.</summary>
public sealed record SpeechProviderHistoryEntry(
    int Version,
    SpeechProviderHistoryAction Action,
    string SnapshotDisplayName,
    SpeechProviderStatus SnapshotStatus,
    SpeechProviderConfig SnapshotConfig,
    DateTimeOffset At,
    Guid? ByUserId);

public enum SpeechProviderHistoryAction
{
    Created,
    Updated,
    StatusChanged,
    SoftDeleted,
}

/// <summary>What actively references this provider — used to gate disable/delete.</summary>
public sealed record SpeechProviderUsage(
    /// <summary>Recipes (active or otherwise) that reference this provider as STT, TTS, or Composite.</summary>
    IReadOnlyList<SpeechProviderUsageRecipeRef> RecipesUsingThisProvider);

public sealed record SpeechProviderUsageRecipeRef(
    string RecipeId,
    string DisplayName,
    bool IsActiveVoiceRecipe);
