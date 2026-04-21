using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Result of resolving an agent for an AG-UI turn.
/// </summary>
/// <param name="Agent">The built agent to run the turn against.</param>
/// <param name="UserBriefPreamble">
/// Optional system message carrying the projected User Brief, to be prepended
/// to the conversation when the agent's descriptor declares
/// <c>RequiresUserBrief</c>. <c>null</c> when the agent does not need a brief
/// or when projection was skipped or failed.
/// </param>
/// <param name="UserBriefCacheStatus">
/// Cache status for the User Brief preamble: <c>hit</c>, <c>miss</c>,
/// <c>skipped</c>, or <c>error</c>.
/// </param>
/// <param name="UserBriefDurationMs">
/// Time spent resolving the User Brief payload. <c>null</c> when the brief was
/// skipped entirely.
/// </param>
public sealed record AgentContextResolution(
    AIAgent Agent,
    ChatMessage? UserBriefPreamble,
    string UserBriefCacheStatus,
    long? UserBriefDurationMs);

/// <summary>
/// Resolves the <see cref="AIAgent"/> to run an AG-UI turn against and, when
/// the agent's descriptor requires it, projects and formats the User Brief as
/// a prependable system message.
/// </summary>
public interface IAgentContextualizer
{
    /// <summary>
    /// Resolves the named domain agent (when <paramref name="agentId"/> is
    /// provided) or the master orchestrator (when it is null). When the
    /// resolved descriptor declares <c>RequiresUserBrief</c> and a user/tenant
    /// are available, projects the user brief and returns it as a system
    /// preamble.
    /// </summary>
    Task<AgentContextResolution> ResolveAsync(string? agentId, CancellationToken cancellationToken);
}
