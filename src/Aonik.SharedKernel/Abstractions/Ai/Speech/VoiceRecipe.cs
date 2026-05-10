namespace Aonik.SharedKernel.Abstractions.Ai.Speech;

/// <summary>
/// A named, savable composition of speech providers + pipeline tweaks. Chained recipes
/// reference one STT and one TTS provider from the library; composite recipes reference one
/// composite provider.
///
/// <para>
/// See <c>docs/specifications/024.unified-speech-config-and-composer.md</c> §"Recipe library".
/// </para>
/// </summary>
public sealed record VoiceRecipe(
    /// <summary>Built-in archetypes use <c>built-in:&lt;name&gt;</c>; tenant rows use Guid.</summary>
    string Id,
    string DisplayName,
    string? Description,
    VoiceRecipeKind Kind,
    /// <summary>Body for chained recipes. Null when <see cref="Kind"/> is Composite.</summary>
    ChainedRecipeBody? Chained,
    /// <summary>Body for composite recipes. Null when <see cref="Kind"/> is Chained.</summary>
    CompositeRecipeBody? Composite,
    bool IsBuiltIn,
    VoiceRecipeStatus Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? CreatedByUserId,
    Guid? LastUpdatedByUserId);

public enum VoiceRecipeKind
{
    Chained,
    Composite,
}

public enum VoiceRecipeStatus
{
    Active,
    Disabled,
    SoftDeleted,
}

/// <summary>
/// Chained pipeline body: STT provider id + TTS provider id + per-recipe voice/model picks +
/// pipeline tweaks. The provider ids are resolved at runtime by <c>AonikVoicePipelineFactory</c>
/// — if either referenced provider was disabled or deleted since the recipe was authored, the
/// connection fails with a clear error.
///
/// <para>
/// Voice + model selection lives here (not on the provider config) so different recipes can
/// run different voices through the same vendor — e.g. one Mistral voice for the formal agent
/// and a different one for the casual agent, both pointing at the same Mistral provider row.
/// </para>
/// </summary>
public sealed record ChainedRecipeBody(
    /// <summary>Stable provider id (built-in or tenant Guid).</summary>
    string SttProviderId,
    string TtsProviderId,
    /// <summary>
    /// Required voice id for the TTS provider (e.g. ElevenLabs voice id, Mistral voice slug,
    /// OpenAI voice name). Validated against the resolved provider when the recipe is saved.
    /// </summary>
    string TtsVoiceId,
    /// <summary>Optional model id override; falls back to the provider's <c>DefaultModelId</c>.</summary>
    string? TtsModelId,
    /// <summary>Optional STT model override; falls back to the provider's <c>DefaultModel</c>.</summary>
    string? SttModel,
    /// <summary>Optional STT language hint (BCP-47); falls back to provider's <c>DefaultLanguage</c>.</summary>
    string? SttLanguage,
    /// <summary>
    /// Optional agent-id pin. When set, this recipe ignores the per-connection
    /// <c>hello.agentId</c> and routes every conversation to the pinned agent. Useful for
    /// kiosk-style deployments. Null = use the client's requested agent (default).
    /// </summary>
    string? PinnedAgentId,
    /// <summary>"energy" (default) | "silero" | "none".</summary>
    string Vad,
    /// <summary>Silence duration before the gate closes. Null = vendor default (800 ms).</summary>
    int? VadStopMs,
    /// <summary>Drop Whisper hallucinations on near-silent audio. Default true.</summary>
    bool TranscriptionFilter,
    /// <summary>Buffer LLM tokens into sentence-sized TTS chunks. Default true.</summary>
    bool SentenceAggregator);

/// <summary>
/// Composite pipeline body: one provider id (must resolve to a Composite provider) plus the
/// per-recipe voice / model / instructions picks. Same separation rationale as
/// <see cref="ChainedRecipeBody"/> — the provider config carries vendor-level defaults, this
/// carries the call-time picks.
/// </summary>
public sealed record CompositeRecipeBody(
    string CompositeProviderId,
    /// <summary>Required voice for the composite engine (e.g. <c>alloy</c>, <c>nova</c>).</summary>
    string Voice,
    /// <summary>Optional model override; falls back to the provider's <c>DefaultModel</c>.</summary>
    string? Model,
    /// <summary>Optional per-recipe instruction addendum; appended to the resolved agent's instructions.</summary>
    string? InstructionsAddendum,
    /// <summary>Optional agent-id pin (same semantics as <see cref="ChainedRecipeBody.PinnedAgentId"/>).</summary>
    string? PinnedAgentId);

public sealed record VoiceRecipeHistoryEntry(
    int Version,
    VoiceRecipeHistoryAction Action,
    string SnapshotDisplayName,
    string? SnapshotDescription,
    VoiceRecipeStatus SnapshotStatus,
    ChainedRecipeBody? SnapshotChained,
    CompositeRecipeBody? SnapshotComposite,
    DateTimeOffset At,
    Guid? ByUserId);

public enum VoiceRecipeHistoryAction
{
    Created,
    Updated,
    StatusChanged,
    SoftDeleted,
}

/// <summary>
/// Service surface for the per-tenant voice recipe library. Built-ins are merged from
/// <see cref="IBuiltInSpeechCatalog.AllRecipes"/>; tenant rows live in <c>AnkVoiceRecipes</c>.
/// </summary>
public interface IVoiceRecipeLibraryService
{
    Task<IReadOnlyList<VoiceRecipe>> ListAsync(
        VoiceRecipeKind? kind = null,
        bool includeDisabled = false,
        CancellationToken cancellationToken = default);

    Task<VoiceRecipe?> GetAsync(string id, CancellationToken cancellationToken = default);

    Task<VoiceRecipe> CreateAsync(CreateVoiceRecipeRequest request, CancellationToken cancellationToken = default);

    Task<VoiceRecipe> UpdateAsync(Guid id, UpdateVoiceRecipeRequest request, CancellationToken cancellationToken = default);

    Task<VoiceRecipe> CloneBuiltInAsync(string builtInId, string? newDisplayName, CancellationToken cancellationToken = default);

    Task<VoiceRecipe> SetStatusAsync(Guid id, VoiceRecipeStatus status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VoiceRecipeHistoryEntry>> GetHistoryAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record CreateVoiceRecipeRequest(
    string DisplayName,
    string? Description,
    VoiceRecipeKind Kind,
    ChainedRecipeBody? Chained,
    CompositeRecipeBody? Composite);

public sealed record UpdateVoiceRecipeRequest(
    string DisplayName,
    string? Description,
    ChainedRecipeBody? Chained,
    CompositeRecipeBody? Composite);
