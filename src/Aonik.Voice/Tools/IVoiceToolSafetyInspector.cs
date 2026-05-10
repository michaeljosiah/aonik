using Microsoft.Extensions.AI;

namespace Aonik.Voice.Tools;

/// <summary>
/// Classifies server-registered backend tools as read-only or mutating for the
/// purpose of building a read-only voice agent variant. v1 enforces the
/// "agents propose, systems execute" rule by filtering mutating tools out of
/// the agent's tool list before any voice connection runs against it.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 1.5 for the
/// classification rules. v1 uses naming-prefix classification
/// (<c>pf_get_</c>, <c>pf_list_</c>, <c>pf_search_</c>, <c>pf_describe_</c>,
/// <c>pf_summarize_</c> = read-only; <c>pf_create_</c>, <c>pf_update_</c>,
/// <c>pf_archive_</c>, <c>pf_delete_</c>, <c>pf_apply_</c>, <c>pf_cancel_</c>,
/// <c>pf_confirm_</c>, <c>pf_reject_</c>, <c>pf_set_</c>, <c>pf_override_</c>
/// = mutating; unknowns are treated as mutating with a warning).
/// </para>
/// </summary>
public interface IVoiceToolSafetyInspector
{
    /// <summary>
    /// Returns the classification of the supplied tool by name.
    /// </summary>
    VoiceToolClassification Classify(string toolName);

    /// <summary>
    /// Filters a tool list to only the read-only tools, suitable for use as
    /// the tool set of a read-only voice agent variant. Logs a warning for any
    /// unclassified tool name (treated as mutating to fail safe).
    /// </summary>
    IReadOnlyList<AITool> FilterReadOnly(IReadOnlyList<AITool> tools);

    /// <summary>
    /// Returns the read-only subset of the supplied tool name list. Use this
    /// to feed <c>IDomainAgentDescriptor.Build(..., allowedToolNames: ...)</c>
    /// — that overload already filters the descriptor's tool list by name.
    /// Unknown names log a warning and are excluded (treated as mutating to
    /// fail safe).
    /// </summary>
    IReadOnlySet<string> FilterReadOnlyNames(IReadOnlyList<string> toolNames);
}

/// <summary>
/// Classification outcome for a single tool name.
/// </summary>
public enum VoiceToolClassification
{
    /// <summary>Read-only — safe for voice. Tool may auto-execute inside MAF.</summary>
    ReadOnly,

    /// <summary>Mutating — must not be exposed to voice without an enforcement wrapper.</summary>
    Mutating,

    /// <summary>Unknown name — treated as mutating to fail safe; classifier should be updated.</summary>
    Unknown,
}
