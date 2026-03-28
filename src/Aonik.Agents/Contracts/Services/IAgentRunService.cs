using Aonik.Agents.Contracts.Models;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Service for querying agent execution history.
/// </summary>
public interface IAgentRunService
{
    /// <summary>
    /// Lists agent runs for the specified agent, ordered by most recent first.
    /// </summary>
    Task<PagedResult<AgentRunSummary>> ListByAgentAsync(
        Guid agentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
