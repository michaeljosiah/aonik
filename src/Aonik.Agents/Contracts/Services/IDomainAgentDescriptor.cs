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
    /// The agent's system instructions/prompt text. Used to seed global defaults
    /// in the Agent configuration table. Returns <c>null</c> if the descriptor
    /// does not expose its instructions.
    /// </summary>
    string? Instructions => null;

    /// <summary>
    /// Builds a <see cref="ChatClientAgent"/> configured with this agent's
    /// name, instructions, and tools.
    /// </summary>
    /// <param name="chatClient">The scoped <see cref="IChatClient"/>.</param>
    /// <param name="serviceProvider">Service provider for resolving domain services.</param>
    AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider);

    /// <summary>
    /// Builds a <see cref="ChatClientAgent"/> with overridden instructions and/or
    /// a filtered tool set. Used by the orchestrator when a tenant-level configuration
    /// override exists in the database.
    /// </summary>
    /// <param name="chatClient">The scoped <see cref="IChatClient"/>.</param>
    /// <param name="serviceProvider">Service provider for resolving domain services.</param>
    /// <param name="instructionsOverride">Overridden system instructions (replaces code-based instructions).</param>
    /// <param name="allowedToolNames">If non-null, only tools whose names are in this set will be included.</param>
    AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
    {
        // Default: delegate to the simple Build and ignore overrides.
        // Descriptors should override this for proper override support.
        return Build(chatClient, serviceProvider);
    }

    /// <summary>
    /// Returns the names of all tools this agent can provide.
    /// Used by the agent configuration system to populate <c>ToolsetIdsJson</c>
    /// in the global default and to validate tenant overrides.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving domain services.</param>
    IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider);
}
