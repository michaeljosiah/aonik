using Microsoft.Agents.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Result of resolving a domain agent: the built agent, its descriptor,
/// and the model name configured for the agent in
/// <c>AnkAgents.AiModelId</c> (or <c>null</c> when none is set, in which
/// case the call falls through to the chat client's global default).
/// Callers stamp <see cref="ConfiguredModelName"/> onto
/// <c>ChatOptions.ModelId</c> at run time so a per-agent model override
/// actually reaches the LLM provider.
/// </summary>
public sealed record DomainAgentResolution(
    AIAgent Agent,
    IDomainAgentDescriptor Descriptor,
    string? ConfiguredModelName);

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
    Task<DomainAgentResolution> ResolveAsync(
        string agentId,
        CancellationToken cancellationToken = default);
}
