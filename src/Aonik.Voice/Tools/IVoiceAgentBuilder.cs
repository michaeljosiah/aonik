using Aonik.SharedKernel.Abstractions.Agents;
using Microsoft.Agents.AI;

namespace Aonik.Voice.Tools;

/// <summary>
/// Builds a read-only voice variant of a resolved AONIK domain agent by
/// filtering its tool list through <see cref="IVoiceToolSafetyInspector"/>
/// and rebuilding the agent via the descriptor's existing
/// <c>Build(..., allowedToolNames)</c> overload. The original agent (used by
/// AGUI chat) is unchanged.
///
/// <para>
/// See <c>docs/specifications/022.aonik-voice-realtime.md</c> Phase 1.5.
/// </para>
/// </summary>
public interface IVoiceAgentBuilder
{
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

/// <summary>
/// Outcome of <see cref="IVoiceAgentBuilder.BuildReadOnlyVariant"/>.
/// </summary>
public sealed record VoiceAgentBuildResult(
    AIAgent Agent,
    IReadOnlySet<string> AllowedToolNames,
    IReadOnlyList<string> RemovedToolNames);
