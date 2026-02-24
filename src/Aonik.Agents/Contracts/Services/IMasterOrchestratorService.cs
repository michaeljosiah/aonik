using Aonik.Agents.Contracts.Models;

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
}
