using System.Text.Json.Serialization;

namespace Aonik.SharedKernel.Abstractions.Ai.Speech;

/// <summary>
/// One configured speech provider in a tenant's library. The model is now <b>one row per
/// (tenant, vendor)</b> — the provider IS the tenant's vendor configuration, including the
/// encrypted API key. Voice + model selection moved off the provider config and onto the
/// consumers (<see cref="VoiceRecipe"/> for live conversations, <see cref="ChatSpeechSettings"/>
/// for chat-reply playback) so different recipes can use different voices for the same vendor.
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
    /// <c>openai-realtime</c>, <c>azure-voice-live</c>. Each tenant may own at most one row
    /// per vendor; create-with-duplicate-vendor is rejected with a clear error.
    /// </summary>
    string Vendor,
    /// <summary>
    /// Vendor-level configuration (region, default model, etc). Voice + model SELECTION lives
    /// on the recipe / chat-speech consumer, not here — this carries vendor-wide defaults that
    /// the consumer can leave blank to inherit.
    /// </summary>
    SpeechProviderConfig Config,
    SpeechProviderStatus Status,
    /// <summary>
    /// True iff a tenant API key is stored on this row. Status-only readback — the encrypted
    /// key itself is never serialized over the wire. Falsey doesn't mean unauthenticated; the
    /// resolver still falls back to host default + configuration fallback if this is false.
    /// </summary>
    bool HasApiKey,
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
///
/// <para>
/// As of the post-Phase-C.2 refactor, <b>voice and model selection are no longer carried here</b>.
/// They moved to <see cref="ChainedRecipeBody"/> / <see cref="CompositeRecipeBody"/> and
/// <see cref="ChatSpeechSettings"/>. What remains is genuinely vendor-level: region pinning,
/// default-model suggestions, and per-vendor tunables like ElevenLabs stability defaults.
/// </para>
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
public sealed record OpenAIWhisperConfig(string? DefaultModel, string? DefaultLanguage) : SpeechProviderConfig;

/// <summary>Azure Speech STT — used by <c>Voxa.Speech.Azure.AzureSpeechToTextEngine</c>.</summary>
public sealed record AzureSttConfig(string Region, string? DefaultLanguage) : SpeechProviderConfig;

// ── TTS configs ─────────────────────────────────────────────────────────────────────────────

/// <summary>OpenAI TTS — used by <c>Voxa.Speech.OpenAI.OpenAITextToSpeechEngine</c>.</summary>
public sealed record OpenAITtsConfig(string? DefaultModelId) : SpeechProviderConfig;

/// <summary>Azure Speech TTS — region is the only vendor-level setting; voice picks live on the recipe.</summary>
public sealed record AzureTtsConfig(string Region) : SpeechProviderConfig;

/// <summary>ElevenLabs TTS — used by <c>Voxa.Speech.ElevenLabs.ElevenLabsTextToSpeechEngine</c>.</summary>
public sealed record ElevenLabsTtsConfig(
    string? DefaultModelId,
    double? DefaultStability,
    double? DefaultSimilarityBoost,
    int? DefaultOptimizeStreamingLatency) : SpeechProviderConfig;

/// <summary>Mistral Voxtral TTS — used by <c>Voxa.Speech.Mistral.MistralTextToSpeechEngine</c>.</summary>
public sealed record MistralTtsConfig(string? DefaultModelId) : SpeechProviderConfig;

// ── Composite configs ───────────────────────────────────────────────────────────────────────

/// <summary>
/// OpenAI Realtime end-to-end composite. Voice + model + per-recipe instructions live on
/// <see cref="CompositeRecipeBody"/>; this carries vendor-level defaults only.
/// </summary>
public sealed record OpenAIRealtimeCompositeConfig(
    string? DefaultModel,
    string? DefaultInstructionsAddendum) : SpeechProviderConfig;

/// <summary>
/// Azure Voice Live end-to-end composite. Endpoint + region are vendor-level (Voice Live is
/// region-pinned); voice + model selection lives on <see cref="CompositeRecipeBody"/>.
/// </summary>
public sealed record AzureVoiceLiveCompositeConfig(
    string Region,
    string Endpoint,
    string? DefaultModel,
    string? DefaultInstructionsAddendum) : SpeechProviderConfig;

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
