using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Voice.Entities;

/// <summary>
/// Persistence shape for a tenant-owned <see cref="VoiceRecipe"/>. Built-ins are not stored
/// (they ship in code via <c>BuiltInVoiceRecipes</c>). Chained vs Composite is encoded by
/// nullable column groups: chained recipes have <c>ChainedSttProviderId</c> populated and
/// <c>CompositeProviderId</c> null, and vice versa.
///
/// <para>
/// The history ring buffer stores prior versions as JSON snapshots in
/// <see cref="PreviousVersionsJson"/>, capped at
/// <see cref="SpeechLibraryConstants.HistoryRetentionPerEntity"/>.
/// </para>
/// </summary>
public sealed class VoiceRecipeEntity : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public VoiceRecipeKind Kind { get; set; }

    // ── Chained body (null when Kind=Composite) ─────────────────────────────────────────

    /// <summary>Stable provider id (built-in like "built-in:openai-whisper-default" OR Guid as string).</summary>
    public string? ChainedSttProviderId { get; set; }
    public string? ChainedTtsProviderId { get; set; }
    /// <summary>Required voice id when Kind=Chained — moved off the provider config.</summary>
    public string? ChainedTtsVoiceId { get; set; }
    /// <summary>Optional per-recipe TTS model override; falls back to provider's <c>DefaultModelId</c>.</summary>
    public string? ChainedTtsModelId { get; set; }
    /// <summary>Optional per-recipe STT model override; falls back to provider's <c>DefaultModel</c>.</summary>
    public string? ChainedSttModel { get; set; }
    /// <summary>Optional per-recipe STT language hint; falls back to provider's <c>DefaultLanguage</c>.</summary>
    public string? ChainedSttLanguage { get; set; }
    public string? ChainedPinnedAgentId { get; set; }
    public string? ChainedVad { get; set; }
    public int? ChainedVadStopMs { get; set; }
    public bool? ChainedTranscriptionFilter { get; set; }
    public bool? ChainedSentenceAggregator { get; set; }

    // ── Composite body (null when Kind=Chained) ─────────────────────────────────────────

    public string? CompositeProviderId { get; set; }
    /// <summary>Required voice when Kind=Composite — moved off the provider config.</summary>
    public string? CompositeVoice { get; set; }
    /// <summary>Optional model override; falls back to provider's <c>DefaultModel</c>.</summary>
    public string? CompositeModel { get; set; }
    /// <summary>Optional per-recipe instruction addendum.</summary>
    public string? CompositeInstructionsAddendum { get; set; }
    public string? CompositePinnedAgentId { get; set; }

    // ── Lifecycle ────────────────────────────────────────────────────────────────────────

    public VoiceRecipeStatus Status { get; set; }
    public int Version { get; set; }
    public string PreviousVersionsJson { get; set; } = "[]";
}
