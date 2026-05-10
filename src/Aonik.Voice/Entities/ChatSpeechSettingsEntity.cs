using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Voice.Entities;

/// <summary>
/// Singleton-per-tenant Chat Speech active settings (spec 024 Phase C). At most one row per
/// tenant; the tenant id is the primary key. Maps to <c>AnkChatSpeechSettings</c> in <c>dbo</c>.
///
/// <para>
/// The runtime cutover (Phase C.2) wires <c>TextToSpeechService</c> to read
/// <see cref="ActiveTtsProviderId"/> from this row instead of the legacy
/// <c>TextToSpeechSettings</c>; today the row is written by the admin UI but the helper-text
/// and AGUI streaming TTS still resolve through the legacy settings page.
/// </para>
/// </summary>
public sealed class ChatSpeechSettingsEntity : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>
    /// Currently selected TTS provider id from the speech library. Null when no provider is
    /// picked. The provider must have <c>Type = Tts</c>; the service rejects anything else.
    /// </summary>
    public string? ActiveTtsProviderId { get; set; }

    /// <summary>
    /// Required when <see cref="ActiveTtsProviderId"/> is non-null; null otherwise. Voice id
    /// is stored on this row (rather than the provider) so the same TTS vendor can be used
    /// for chat replies with one voice and for a voice-mode recipe with another voice.
    /// </summary>
    public string? ActiveTtsVoiceId { get; set; }

    /// <summary>Optional per-tenant model override; null falls back to provider default.</summary>
    public string? ActiveTtsModelId { get; set; }

    public bool Enabled { get; set; } = true;

    /// <summary>Speak each reply automatically as it arrives. Default off (operator opt-in).</summary>
    public bool AutoPlay { get; set; }

    /// <summary>Show a speaker icon next to each chat reply for manual playback.</summary>
    public bool ShowSpeakButton { get; set; } = true;

    /// <summary>
    /// Playback rate as a percentage of natural pace. 100 = 1.0x, 150 = 1.5x. Stored as int
    /// to avoid floating-point comparisons in DB queries; range 50–200 enforced at the service
    /// layer.
    /// </summary>
    public int RatePercent { get; set; } = 100;
}
