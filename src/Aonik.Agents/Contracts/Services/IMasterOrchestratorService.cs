using Aonik.Agents.Contracts.Models;
using Microsoft.Agents.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Master orchestrator service. Routes user messages to the appropriate
/// domain agents using the agent-as-tool pattern, where each domain agent
/// (finance, platform, etc.) is exposed as a callable function tool to the
/// master agent.
/// </summary>
public interface IMasterOrchestratorService
{
    /// <summary>
    /// Sends a user message to the master orchestrator agent and returns
    /// the agent's response. The orchestrator determines which domain agent(s)
    /// to invoke based on the user's intent.
    /// </summary>
    /// <param name="request">The chat request containing the user's message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The orchestrator's response.</returns>
    Task<AgentChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the built orchestrator as an <see cref="AIAgent"/> suitable for
    /// AG-UI protocol hosting via <c>MapAGUI</c>. The agent is built lazily on
    /// first call and cached for reuse.
    /// </summary>
    Task<AIAgent> GetAgentAsync(CancellationToken cancellationToken = default);
}
