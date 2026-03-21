using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Contracts.Services;

/// <summary>
/// Describes a domain agent and can build a MAF <see cref="ChatClientAgent"/>
/// on demand. Registered as keyed singleton services where the key is the agent name.
/// The orchestrator resolves all registered descriptors to compose agents-as-tools.
/// </summary>
public interface IDomainAgentDescriptor
{
    /// <summary>Agent name used as the keyed service key (e.g. "finance-agent").</summary>
    string Name { get; }

    /// <summary>Agent description for use in agent-as-tool composition.</summary>
    string Description { get; }

    /// <summary>
    /// Builds a <see cref="ChatClientAgent"/> configured with this agent's
    /// name, instructions, and tools.
    /// </summary>
    /// <param name="chatClient">The scoped <see cref="IChatClient"/>.</param>
    /// <param name="serviceProvider">Service provider for resolving domain services.</param>
    AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider);
}
