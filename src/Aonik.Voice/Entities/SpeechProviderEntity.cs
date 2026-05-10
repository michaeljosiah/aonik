using Aonik.SharedKernel.Abstractions.Ai.Speech;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Voice.Entities;

/// <summary>
/// Persistence shape for a tenant-owned <see cref="SpeechProvider"/>. Built-in archetypes are
/// not stored — they ship in code via <see cref="Library.BuiltInSpeechCatalog"/> and are merged
/// into list responses by the library service.
///
/// <para>
/// Polymorphic <see cref="SpeechProviderConfig"/> is serialized as JSON into
/// <see cref="ConfigJson"/>. The history ring buffer is serialized as a JSON array into
/// <see cref="PreviousVersionsJson"/> — see
/// <see cref="SpeechLibraryConstants.HistoryRetentionPerEntity"/> for the cap.
/// </para>
/// </summary>
public sealed class SpeechProviderEntity : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    /// <summary>Admin-given display name. Required, max 200 chars.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>STT, TTS, or Composite. Stored as string for readability in DB queries.</summary>
    public SpeechProviderType Type { get; set; }

    /// <summary>Vendor shortcode (openai, azure, elevenlabs, mistral, openai-realtime, azure-voice-live).</summary>
    public string Vendor { get; set; } = string.Empty;

    /// <summary>Polymorphic <see cref="SpeechProviderConfig"/> serialized as JSON. nvarchar(max).</summary>
    public string ConfigJson { get; set; } = "{}";

    public SpeechProviderStatus Status { get; set; }

    /// <summary>
    /// Increments on every Update / StatusChanged. Created rows start at 1; soft-deleted rows
    /// keep their last version. Built-ins (not persisted here) are always Version 1.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// JSON array of <see cref="SpeechProviderHistoryEntry"/>. Newest first, capped to
    /// <see cref="SpeechLibraryConstants.HistoryRetentionPerEntity"/>. Older snapshots roll off.
    /// </summary>
    public string PreviousVersionsJson { get; set; } = "[]";
}
