using Microsoft.Extensions.AI;

namespace Aonik.Voice.Tools;

/// <summary>
/// v1 catalog — hardcoded allowlist matching the 8 names in
/// <c>apps/payabo_mobile/lib/data/repositories/live_chat_repository.dart</c>.
/// Phase 4 of the spec extracts that mobile-side registry to a shared module;
/// this server catalog should track the same names.
///
/// <para>
/// Canonical <see cref="AITool"/> declarations are stubbed for v1 and will be
/// fleshed out in Phase 3 once <see cref="VoiceFrontendToolCatalog"/> is wired
/// into the agent run-options builder.
/// </para>
/// </summary>
internal sealed class VoiceFrontendToolCatalog : IVoiceFrontendToolCatalog
{
    private static readonly HashSet<string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        "confirmAction",
        "display_fx_rate_chart",
        "display_budget_breakdown",
        "display_spending_pie_chart",
        "display_autopilot_proposal",
        "display_follow_up_suggestions",
        "display_option_selector",
        "navigate_to_screen",
    };

    public IReadOnlySet<string> AllowedNames => Names;

    public IReadOnlyList<AITool> ResolveCanonical(IReadOnlyList<string> names)
    {
        // TODO (Phase 3): build canonical AITool declarations using
        // AIFunctionFactory.Create(...) with the documented arg schemas for
        // each frontend tool. Voice has no server-side implementation, so the
        // function bodies are no-ops that throw — MAF surfaces the calls as
        // FunctionCallContent without execution.
        _ = names;
        return Array.Empty<AITool>();
    }

    public IReadOnlyList<string> Validate(IReadOnlyList<string> names)
    {
        if (names is null || names.Count == 0)
            return Array.Empty<string>();

        var rejected = new List<string>();
        foreach (var name in names)
        {
            if (!Names.Contains(name))
                rejected.Add(name);
        }
        return rejected;
    }
}
