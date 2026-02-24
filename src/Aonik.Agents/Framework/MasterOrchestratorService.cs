using System.Collections.Concurrent;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Master orchestrator that uses the agent-as-tool pattern. Each registered
/// <see cref="AonikDomainAgent"/> is built into a MAF <see cref="AIAgent"/>
/// and then exposed as a function tool (via <c>AsAIFunction()</c>) to a
/// top-level orchestrator agent.
///
/// The orchestrator's LLM decides which domain agent to call based on the
/// user's intent. This approach means the orchestrator agent retains overall
/// responsibility and context while delegating specific domain tasks.
///
/// Session management uses an in-memory <see cref="ConcurrentDictionary{TKey,TValue}"/>
/// keyed by session ID to maintain multi-turn conversation history.
/// </summary>
internal sealed class MasterOrchestratorService : IMasterOrchestratorService
{
    private readonly IEnumerable<AonikDomainAgent> _domainAgents;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MasterOrchestratorService> _logger;

    /// <summary>
    /// In-memory session store: sessionId -> list of ChatMessages.
    /// This is intentionally simple for now; a durable session store
    /// (e.g., backed by AgentsDbContext) can replace this later.
    /// </summary>
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> Sessions = new();

    private const string OrchestratorInstructions =
        """
        You are the AONIK Master Orchestrator. You help users accomplish tasks across
        the AONIK financial operating system by routing their requests to the appropriate
        domain agents.

        Available domain agents are provided as function tools. Each domain agent is a
        specialist in its area:

        - **finance-agent**: Manages invoices, ledger accounts, journal entries, and payment
          intents. Use this for any billing, accounting, or payment-related requests.
        - **platform-agent**: Manages tenants, users, roles, permissions, and compliance
          documents. Use this for identity, access management, or compliance-related requests.

        Rules:
        1. Analyse the user's request and determine which domain agent(s) to invoke.
        2. If the request spans multiple domains, call the relevant agents in sequence.
        3. Synthesise the results from domain agents into a clear, coherent response.
        4. If you are unsure which agent to use, ask the user for clarification.
        5. Never fabricate data — only report information returned by the domain agents.
        6. Present monetary amounts with their currency code.
        7. Reference entities by their IDs for clarity.
        8. If an operation fails, explain the error and suggest corrective action.
        """;

    public MasterOrchestratorService(
        IEnumerable<AonikDomainAgent> domainAgents,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        ILogger<MasterOrchestratorService> logger)
    {
        _domainAgents = domainAgents;
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<AgentChatResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N");

        _logger.LogInformation(
            "Orchestrator processing message for session {SessionId}",
            sessionId);

        // Get or create session history
        var history = Sessions.GetOrAdd(sessionId, _ => new List<ChatMessage>());

        // Build domain agents as tools
        var domainTools = BuildDomainAgentTools();

        _logger.LogInformation(
            "Orchestrator has {ToolCount} domain agent tool(s): {ToolNames}",
            domainTools.Count,
            string.Join(", ", domainTools.Select(t => t.Name)));

        // Build the orchestrator agent with domain agents as tools
        var orchestratorAgent = new ChatClientAgent(
            _chatClient,
            name: "master-orchestrator",
            instructions: OrchestratorInstructions,
            tools: domainTools);

        // Add the user's message to history
        var userMessage = new ChatMessage(ChatRole.User, request.Message);

        // Build the full message list (history + new message)
        List<ChatMessage> messages;
        lock (history)
        {
            history.Add(userMessage);
            messages = new List<ChatMessage>(history);
        }

        // Run the orchestrator
        var response = await orchestratorAgent.RunAsync(
            messages,
            cancellationToken: cancellationToken);

        // Extract the response text
        var responseText = response.Text ?? string.Empty;

        // Add assistant response to history for multi-turn
        if (response.Messages.Count > 0)
        {
            lock (history)
            {
                history.AddRange(response.Messages);
            }
        }

        _logger.LogInformation(
            "Orchestrator completed for session {SessionId}. Response length: {Length}",
            sessionId, responseText.Length);

        return new AgentChatResponse
        {
            Message = responseText,
            SessionId = sessionId,
            AgentName = "master-orchestrator"
        };
    }

    /// <summary>
    /// Builds each registered domain agent and wraps it as an <see cref="AITool"/>
    /// via <c>AsAIFunction()</c>. The orchestrator LLM can then invoke any domain
    /// agent as a function call.
    /// </summary>
    private List<AITool> BuildDomainAgentTools()
    {
        var tools = new List<AITool>();

        foreach (var domainAgent in _domainAgents)
        {
            try
            {
                var builtAgent = domainAgent.Build(_chatClient, _serviceProvider);
                var agentTool = builtAgent.AsAIFunction();
                tools.Add(agentTool);

                _logger.LogDebug(
                    "Built domain agent tool: {AgentName} — {Description}",
                    domainAgent.Name, domainAgent.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to build domain agent '{AgentName}' as tool",
                    domainAgent.Name);
            }
        }

        return tools;
    }
}
