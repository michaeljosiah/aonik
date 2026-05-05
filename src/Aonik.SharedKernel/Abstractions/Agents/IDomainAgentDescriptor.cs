using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.SharedKernel.Abstractions.Agents;

/// <summary>
/// Describes a domain agent and can build a <see cref="ChatClientAgent"/>
/// on demand. Domain modules (Platform, Finance, …) implement this contract
/// to register their agents; the Agents runtime discovers all registrations
/// via DI (<c>IEnumerable&lt;IDomainAgentDescriptor&gt;</c>) and composes
/// them as agents-as-tools.
/// </summary>
/// <remarks>
/// Lives on SharedKernel so that domain modules contributing an agent do
/// not take a back-pointing reference on the Agents runtime — the runtime
/// can be swapped or unloaded without touching Platform / Finance.
/// </remarks>
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
    /// When <c>true</c>, the AG-UI endpoint will project and inject the User Brief
    /// as a system message before the conversation history on every request.
    /// Only agents that are user-facing product agents (e.g. Payabo's Simi) should
    /// set this — admin and platform agents do not need per-user financial context.
    /// </summary>
    bool RequiresUserBrief => false;

    /// <summary>
    /// Builds a <see cref="ChatClientAgent"/> configured with this agent's
    /// name, instructions, and tools.
    /// </summary>
    /// <param name="chatClient">The scoped <see cref="IChatClient"/>.</param>
    /// <param name="serviceProvider">Service provider for resolving domain services.</param>
    AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider);

    /// <summary>
    /// Classifies the agent as a user-facing orchestrator or an internal
    /// specialist sub-agent.
    /// </summary>
    AgentType AgentType => AgentType.SubAgent;

    /// <summary>
    /// Optional JSON schema describing the structured output this agent returns.
    /// Primarily used for specialist agents that produce schema-bound results.
    /// </summary>
    string? OutputSchemaJson => null;

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
