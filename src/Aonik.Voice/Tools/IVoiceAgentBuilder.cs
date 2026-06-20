using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;

namespace Aonik.Voice.Tools;

/// <summary>
/// Builds the voice variant of a resolved AONIK domain agent. Two shapes (the original agent used
/// by AG-UI chat is never mutated):
/// <list type="bullet">
///   <item><see cref="BuildVariant"/> — the Spec 032 default. If the descriptor's mutating tools are
///   classified by the approval manifest, it builds the <em>full</em> gated agent (every mutating
///   tool wrapped by the server-side approval gate), so Medium/High tools are exposed over voice and
///   enforced server-side. Otherwise it falls back to the read-only variant.</item>
///   <item><see cref="BuildReadOnlyVariant"/> — the original Phase 1.5 behaviour: filter the tool
///   list through <see cref="IVoiceToolSafetyInspector"/> and rebuild with only the read-only subset.
///   Retained as the fallback for agents with unclassified mutating tools.</item>
/// </list>
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 1.5 and
/// <c>docs/specifications/032.tiered-ai-mutation-approval.html</c> §7.7.
/// </para>
/// </summary>
public interface IVoiceAgentBuilder
{
    /// <summary>
    /// Produces the voice variant for <paramref name="descriptor"/>, choosing the full gated toolset
    /// when the agent's mutating tools are classified (so the approval gate can enforce Medium/High
    /// server-side and the client renders an approval card), or the read-only variant otherwise.
    /// </summary>
    VoiceAgentBuildResult BuildVariant(
        IDomainAgentDescriptor descriptor,
        IServiceProvider serviceProvider);

    /// <summary>
    /// Produces a read-only voice variant of the agent described by
    /// <paramref name="descriptor"/>. Returns the agent and the set of tool
    /// names that survived the filter (so callers can stamp telemetry / verify
    /// nothing leaked).
    /// </summary>
    VoiceAgentBuildResult BuildReadOnlyVariant(
        IDomainAgentDescriptor descriptor,
        IServiceProvider serviceProvider);
}

/// <summary>How the voice variant was assembled.</summary>
public enum VoiceAgentToolMode
{
    /// <summary>Read-only subset only — mutating tools were filtered out (fallback / no classified mutations).</summary>
    ReadOnly,

    /// <summary>Full toolset with every mutating tool wrapped by the server-side approval gate (Spec 032).</summary>
    Gated,
}

/// <summary>
/// Outcome of <see cref="IVoiceAgentBuilder.BuildVariant"/> /
/// <see cref="IVoiceAgentBuilder.BuildReadOnlyVariant"/>.
/// </summary>
public sealed record VoiceAgentBuildResult(
    AIAgent Agent,
    IReadOnlySet<string> AllowedToolNames,
    IReadOnlyList<string> RemovedToolNames,
    VoiceAgentToolMode ToolMode = VoiceAgentToolMode.ReadOnly);
