using System.Collections.Concurrent;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Aonik.Agents.Framework;

/// <summary>
/// Master orchestrator that uses the agent-as-tool pattern. Each registered
/// <see cref="IDomainAgentDescriptor"/> is built into a MAF <see cref="AIAgent"/>
/// and then exposed as a function tool (via <c>AsAIFunction()</c>) to a
/// top-level orchestrator agent.
///
/// The orchestrator's LLM decides which domain agent to call based on the
/// user's intent. This approach means the orchestrator agent retains overall
/// responsibility and context while delegating specific domain tasks.
///
/// Session management uses MAF's <see cref="AgentSession"/> per session ID,
/// which tracks conversation history natively. The built orchestrator agent is
/// cached and reused across requests (tools are built once, not per-request).
/// </summary>
internal sealed class MasterOrchestratorService : IMasterOrchestratorService
{
    private readonly IEnumerable<IDomainAgentDescriptor> _descriptors;
    private readonly IMcpToolProvider _mcpToolProvider;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MasterOrchestratorService> _logger;

    /// <summary>
    /// In-memory session store: sessionId -> MAF session.
    /// <see cref="AgentSession"/> tracks conversation history natively,
    /// eliminating the need for manual message list management.
    /// A durable session store (e.g., backed by AgentsDbContext) can replace this later.
    /// </summary>
    private static readonly ConcurrentDictionary<string, AgentSession> Sessions = new();

    /// <summary>
    /// Cached orchestrator agent. Built once (lazily) and reused across requests.
    /// The agent itself is stateless; session state is managed via <see cref="AgentSession"/>.
    /// </summary>
    private ChatClientAgent? _cachedOrchestrator;
    private readonly SemaphoreSlim _buildLock = new(1, 1);

    private const string OrchestratorInstructions =
        """
        You are the AONIK Master Orchestrator. You help users accomplish tasks across
        the AONIK financial operating system by routing their requests to the appropriate
        domain agents.

        Available domain agents are provided as function tools. Each domain agent is a
        specialist in its area:

        - **finance-agent**: Manages invoices, ledger accounts, journal entries, and payment
          intents. Use this for any billing, accounting, or payment-related requests.
        - **financial-life-graph-agent**: Manages the Financial Life Graph — a knowledge graph
          of financial entities, relationships, and insights. Use this for holistic financial
          views, relationship queries, impact analysis, and financial planning.
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
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IMcpToolProvider mcpToolProvider,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        ILogger<MasterOrchestratorService> logger)
    {
        _descriptors = descriptors;
        _mcpToolProvider = mcpToolProvider;
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

        // Ensure the orchestrator agent is built and cached
        var orchestrator = await GetOrBuildOrchestratorAsync(cancellationToken);

        // Get or create MAF session for this conversation.
        // CreateSessionAsync produces a ChatClientAgentSession that tracks
        // conversation history natively — no manual message list management needed.
        var session = await GetOrCreateSessionAsync(sessionId, orchestrator, cancellationToken);

        // Run the orchestrator with the user's message and session.
        var response = await orchestrator.RunAsync(
            request.Message,
            session,
            cancellationToken: cancellationToken);

        var responseText = response.Text ?? string.Empty;

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
    /// Gets or creates a MAF session for the given session ID.
    /// Uses <see cref="ChatClientAgent.CreateSessionAsync"/> to create sessions
    /// that natively track conversation history.
    /// </summary>
    private async Task<AgentSession> GetOrCreateSessionAsync(
        string sessionId,
        ChatClientAgent orchestrator,
        CancellationToken cancellationToken)
    {
        if (Sessions.TryGetValue(sessionId, out var existing))
            return existing;

        // Create a new session via the agent. This returns a ChatClientAgentSession
        // that tracks messages internally across RunAsync calls.
        var session = await orchestrator.CreateSessionAsync(
            sessionId, cancellationToken);

        // Cache it. If another thread raced us, use the winner's session.
        return Sessions.GetOrAdd(sessionId, session);
    }

    /// <summary>
    /// Returns the cached orchestrator agent, building it on first call.
    /// The agent is stateless and can be safely reused across requests;
    /// per-conversation state lives in <see cref="AgentSession"/>.
    /// </summary>
    private async Task<ChatClientAgent> GetOrBuildOrchestratorAsync(
        CancellationToken cancellationToken)
    {
        if (_cachedOrchestrator is not null)
            return _cachedOrchestrator;

        await _buildLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedOrchestrator is not null)
                return _cachedOrchestrator;

            var tools = await BuildAllToolsAsync(cancellationToken);

            _logger.LogInformation(
                "Building orchestrator with {ToolCount} tool(s): {ToolNames}",
                tools.Count,
                string.Join(", ", tools.Select(t => t.Name)));

            _cachedOrchestrator = new ChatClientAgent(
                _chatClient,
                name: "master-orchestrator",
                instructions: OrchestratorInstructions,
                tools: tools);

            return _cachedOrchestrator;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    /// <summary>
    /// Builds the complete tool set for the orchestrator: domain agents-as-tools
    /// plus any MCP-provided tools.
    /// </summary>
    private async Task<List<AITool>> BuildAllToolsAsync(CancellationToken cancellationToken)
    {
        var tools = new List<AITool>();

        // Build domain agents as tools (R1: uses IDomainAgentDescriptor)
        foreach (var descriptor in _descriptors)
        {
            try
            {
                var builtAgent = descriptor.Build(_chatClient, _serviceProvider);
                var agentTool = builtAgent.AsAIFunction();
                tools.Add(agentTool);

                _logger.LogDebug(
                    "Built domain agent tool: {AgentName} — {Description}",
                    descriptor.Name, descriptor.Description);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to build domain agent '{AgentName}' as tool",
                    descriptor.Name);
            }
        }

        // Wire in MCP tools (R8: McpToolProvider was registered but unused)
        try
        {
            var allMcpTools = await _mcpToolProvider.GetAllToolsAsync(cancellationToken);
            foreach (var (serverName, serverTools) in allMcpTools)
            {
                tools.AddRange(serverTools);
                _logger.LogInformation(
                    "Added {ToolCount} MCP tool(s) from server '{ServerName}'",
                    serverTools.Count, serverName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load MCP tools — orchestrator will operate without external tools");
        }

        return tools;
    }
}
