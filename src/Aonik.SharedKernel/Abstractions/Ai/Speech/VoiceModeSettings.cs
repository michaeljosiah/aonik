namespace Aonik.SharedKernel.Abstractions.Ai.Speech;

/// <summary>
/// Per-tenant Voice Mode configuration. Picks which <see cref="VoiceRecipe"/> drives the live
/// spoken conversation experience and whether the experience is enabled at all. There is at
/// most one row per tenant — the entity is a singleton-per-tenant settings record, not a
/// collection.
///
/// <para>
/// Spec 024 Phase C. The earlier voice settings flow lives at <c>/settings/voice</c> and
/// continues to drive the runtime until the <c>AonikVoicePipelineFactory</c> rewire (Phase C.2).
/// </para>
/// </summary>
public sealed record VoiceModeSettings(
    /// <summary>Currently active recipe id (built-in id or tenant Guid). Null = no recipe selected.</summary>
    string? ActiveRecipeId,
    /// <summary>Workspace-wide on/off switch for the spoken conversation experience.</summary>
    bool Enabled,
    DateTimeOffset UpdatedAt,
    Guid? LastUpdatedByUserId);

/// <summary>
/// Update payload. The frontend sends both fields on every save — partial updates aren't
/// supported (the form doesn't model "leave unchanged").
/// </summary>
public sealed record UpdateVoiceModeSettingsRequest(
    string? ActiveRecipeId,
    bool Enabled);

/// <summary>
/// Per-tenant Chat Speech configuration. Picks which TTS provider + voice from the speech
/// library is used to read written chat replies aloud, plus the playback ergonomics (auto-play,
/// speaker button visibility, rate). Independent of <see cref="VoiceModeSettings"/> — the two
/// flows share providers but configure separately.
///
/// <para>
/// Voice + model selection is on this row (not the provider) so the same vendor can drive a
/// different chat voice than what's used in any voice-mode recipe.
/// </para>
/// </summary>
public sealed record ChatSpeechSettings(
    /// <summary>Currently selected TTS provider id from the library. Null = no provider picked.</summary>
    string? ActiveTtsProviderId,
    /// <summary>Required when <see cref="ActiveTtsProviderId"/> is non-null; null otherwise.</summary>
    string? ActiveTtsVoiceId,
    /// <summary>Optional per-tenant model override; null falls back to provider default.</summary>
    string? ActiveTtsModelId,
    bool Enabled,
    /// <summary>Speak each reply automatically as it arrives.</summary>
    bool AutoPlay,
    /// <summary>Show a speaker icon next to each chat reply for manual playback.</summary>
    bool ShowSpeakButton,
    /// <summary>Playback rate as a percentage of natural pace. 100 = 1.0x; range 50–200.</summary>
    int RatePercent,
    DateTimeOffset UpdatedAt,
    Guid? LastUpdatedByUserId);

public sealed record UpdateChatSpeechSettingsRequest(
    string? ActiveTtsProviderId,
    string? ActiveTtsVoiceId,
    string? ActiveTtsModelId,
    bool Enabled,
    bool AutoPlay,
    bool ShowSpeakButton,
    int RatePercent);

/// <summary>
/// Read/write API for Voice Mode active settings. Implementations apply tenant scoping via
/// the ambient <c>ITenantProvider</c>; callers don't need to pass a tenant id explicitly.
/// </summary>
public interface IVoiceModeSettingsService
{
    /// <summary>Get the current settings, returning sensible defaults if no row exists yet.</summary>
    Task<VoiceModeSettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert the settings for the current tenant. Validates that the referenced recipe id
    /// (when non-null) resolves to a known recipe; otherwise throws
    /// <see cref="SpeechLibraryValidationException"/>.
    /// </summary>
    Task<VoiceModeSettings> UpdateAsync(
        UpdateVoiceModeSettingsRequest request,
        CancellationToken cancellationToken = default);
}

public interface IChatSpeechSettingsService
{
    Task<ChatSpeechSettings> GetAsync(CancellationToken cancellationToken = default);

    Task<ChatSpeechSettings> UpdateAsync(
        UpdateChatSpeechSettingsRequest request,
        CancellationToken cancellationToken = default);
}
