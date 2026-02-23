using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Base class for AONIK domain agents. Each domain agent defines its name,
/// instructions (system prompt), and the tools it exposes. The agent uses
/// an <see cref="IChatClient"/> (resolved via the AI module) to interact
/// with an LLM.
/// 
/// Concrete agents (FinanceDomainAgent, PlatformDomainAgent, etc.) override
/// the abstract members to provide domain-specific behaviour.
/// </summary>
internal abstract class AonikDomainAgent
{
    /// <summary>Agent display name (e.g. "finance-agent").</summary>
    protected abstract string Name { get; }

    /// <summary>System prompt / instructions for the LLM.</summary>
    protected abstract string Instructions { get; }

    /// <summary>
    /// Returns the set of tools (functions) that this agent can invoke.
    /// Each tool is created via <see cref="AIFunctionFactory.Create"/> and
    /// exposes a domain service method safely.
    /// </summary>
    protected abstract IEnumerable<AITool> GetTools(IServiceProvider serviceProvider);

    /// <summary>
    /// Sends a user message to the agent and returns the LLM response.
    /// Tools are automatically invoked by the <see cref="IChatClient"/> pipeline
    /// (when configured with function-calling support).
    /// </summary>
    public virtual async Task<ChatResponse> InvokeAsync(
        IChatClient chatClient,
        string userMessage,
        IServiceProvider serviceProvider,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        var tools = GetTools(serviceProvider).ToList();

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, Instructions),
            new(ChatRole.User, userMessage)
        };

        var options = new ChatOptions
        {
            Tools = tools
        };

        logger?.LogInformation("Agent '{AgentName}' invoking with {ToolCount} tools", Name, tools.Count);

        var response = await chatClient.GetResponseAsync(messages, options, cancellationToken);

        logger?.LogInformation("Agent '{AgentName}' completed. Response length: {Length}",
            Name, response.Text?.Length ?? 0);

        return response;
    }
}
