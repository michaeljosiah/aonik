using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Aonik.Agents.Framework;

/// <summary>
/// Base class for AONIK domain agent definitions. Each domain agent defines its
/// name, instructions (system prompt), and the tools it exposes. The <see cref="Build"/>
/// method creates a MAF <see cref="ChatClientAgent"/> configured with the domain-specific
/// tools and instructions.
///
/// Concrete agents (FinanceDomainAgent, PlatformDomainAgent, etc.) override
/// the abstract members to provide domain-specific behaviour.
///
/// The returned <see cref="AIAgent"/> is invoked via <c>agent.RunAsync()</c> or
/// <c>agent.RunStreamingAsync()</c> and can be composed as a tool for a master
/// orchestrator via <c>agent.AsAIFunction()</c>.
/// </summary>
public abstract class AonikDomainAgent
{
    /// <summary>Agent display name (e.g. "finance-agent").</summary>
    public abstract string Name { get; }

    /// <summary>Agent description for use in agent-as-tool composition.</summary>
    public virtual string Description => $"AONIK {Name} domain agent";

    /// <summary>System prompt / instructions for the LLM.</summary>
    protected abstract string Instructions { get; }

    /// <summary>
    /// Returns the set of tools (functions) that this agent can invoke.
    /// Each tool is created via <see cref="AIFunctionFactory.Create"/> and
    /// exposes a domain service method safely.
    /// </summary>
    protected abstract IEnumerable<AITool> GetTools(IServiceProvider serviceProvider);

    /// <summary>
    /// Builds a MAF <see cref="ChatClientAgent"/> configured with this agent's
    /// name, instructions, and tools. The <paramref name="chatClient"/> should
    /// already have any middleware (audit, proposal) applied in its pipeline.
    /// </summary>
    /// <param name="chatClient">
    /// The <see cref="IChatClient"/> resolved from the AI module (possibly with
    /// <see cref="AuditMiddleware"/> and <see cref="ProposalMiddleware"/> in its pipeline).
    /// </param>
    /// <param name="serviceProvider">
    /// Service provider used to resolve domain services for tool creation.
    /// </param>
    /// <returns>
    /// A fully configured <see cref="AIAgent"/> ready to be invoked via
    /// <c>RunAsync</c> / <c>RunStreamingAsync</c>, or composed as a tool
    /// via <c>AsAIFunction()</c>.
    /// </returns>
    public virtual AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
    {
        var tools = GetTools(serviceProvider).ToList();

        return new ChatClientAgent(
            chatClient,
            name: Name,
            instructions: Instructions,
            tools: tools);
    }
}
