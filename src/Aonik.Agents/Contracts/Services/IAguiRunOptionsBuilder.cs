using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Builds <see cref="ChatClientAgentRunOptions"/> for an AG-UI run by
/// combining client-side tool declarations with the agent's per-config
/// model override. Returns <c>null</c> when neither a tool list nor a
/// model override is set — agents without options inherit the chat
/// client's global default model.
/// </summary>
public interface IAguiRunOptionsBuilder
{
    /// <summary>
    /// Compose run options from the client tool list and the model name
    /// resolved by <c>IAgentContextualizer</c>. Returns <c>null</c> when
    /// the result would be a no-op (no tools, no model override).
    /// </summary>
    ChatClientAgentRunOptions? Build(
        IReadOnlyList<AITool> clientTools,
        string? configuredModelName);
}
