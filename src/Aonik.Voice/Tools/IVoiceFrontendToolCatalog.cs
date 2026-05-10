using Microsoft.Extensions.AI;

namespace Aonik.Voice.Tools;

/// <summary>
/// Server-owned allowlist of frontend tool names the voice model is permitted
/// to call. Stricter than AGUI's client-supplied tool POST behavior because
/// voice is a persistent authenticated socket and tool declarations influence
/// model behavior.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> "Frontend Tool
/// Catalog" section. v1 enumerates 8 known names: <c>confirmAction</c>,
/// <c>display_fx_rate_chart</c>, <c>display_budget_breakdown</c>,
/// <c>display_spending_pie_chart</c>, <c>display_autopilot_proposal</c>,
/// <c>display_follow_up_suggestions</c>, <c>display_option_selector</c>,
/// <c>navigate_to_screen</c>.
/// </para>
/// </summary>
public interface IVoiceFrontendToolCatalog
{
    /// <summary>The set of allowed frontend tool names. Used for hello validation.</summary>
    IReadOnlySet<string> AllowedNames { get; }

    /// <summary>
    /// Returns the canonical <see cref="AITool"/> declarations for the supplied
    /// names, filtered to allowed names. Names not in <see cref="AllowedNames"/>
    /// are silently dropped (caller should validate first via
    /// <see cref="Validate(IReadOnlyList{string})"/>).
    /// </summary>
    IReadOnlyList<AITool> ResolveCanonical(IReadOnlyList<string> names);

    /// <summary>
    /// Validates the supplied names against the allowlist. Returns the list of
    /// rejected names; empty list means everything is allowed.
    /// </summary>
    IReadOnlyList<string> Validate(IReadOnlyList<string> names);
}
