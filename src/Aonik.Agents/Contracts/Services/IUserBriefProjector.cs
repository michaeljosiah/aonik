using Aonik.Agents.Contracts.Models;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Assembles the user brief by projecting data from existing domain sources
/// plus user memory and conversation history into a compact JSON payload
/// ready for AI agent consumption.
/// </summary>
public interface IUserBriefProjector
{
    /// <summary>
    /// Assembles the full user brief for an agent session.
    /// </summary>
    Task<UserBrief> ProjectAsync(
        Guid tenantId,
        Guid userId,
        UserBriefOptions? options = null,
        CancellationToken cancellationToken = default);
}
