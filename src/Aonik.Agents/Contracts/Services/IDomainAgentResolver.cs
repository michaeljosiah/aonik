using Microsoft.Agents.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Resolves a named <see cref="IDomainAgentDescriptor"/>, applies any
/// database-level configuration overrides, and builds the domain agent.
/// </summary>
/// <remarks>
/// Registered as a scoped service. Results are memoised within the scope
/// so that multiple calls in the same request (for example, streaming
/// setup + post-stream persistence) do not rebuild the agent or re-read
/// the configuration from the database.
///
/// Not safe to cache across scopes because the built <see cref="AIAgent"/>
/// closes over scoped services (<see cref="Microsoft.Extensions.AI.IChatClient"/>
/// and tool dependencies that hold DbContext references).
/// </remarks>
public interface IDomainAgentResolver
{
    Task<(AIAgent Agent, IDomainAgentDescriptor Descriptor)> ResolveAsync(
        string agentId,
        CancellationToken cancellationToken = default);
}
