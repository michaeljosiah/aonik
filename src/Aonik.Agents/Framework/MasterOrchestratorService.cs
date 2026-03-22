using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Aonik.Agents.Contracts.Models;
using Aonik.Agents.Contracts.Services;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ai;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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
    private readonly IAgentConfigurationService _configService;
    private readonly IChatClient _chatClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserProvider _currentUserProvider;
    private readonly ILogger<MasterOrchestratorService> _logger;
    private readonly bool _enableSensitiveData;

    /// <summary>
    /// In-memory session store: sessionId -> MAF session.
    /// <see cref="AgentSession"/> tracks conversation history natively,
    /// eliminating the need for manual message list management.
    /// A durable session store (e.g., backed by AgentsDbContext) can replace this later.
    /// </summary>
    private static readonly ConcurrentDictionary<string, AgentSession> Sessions = new();

    /// <summary>
    /// Cached orchestrator agent (OTel-instrumented). Built once (lazily) and reused
    /// across requests within the same scope. The agent itself is stateless; session
    /// state is managed via <see cref="AgentSession"/>.
    /// </summary>
    private AIAgent? _cachedOrchestrator;

    /// <summary>
    /// Raw <see cref="ChatClientAgent"/> used exclusively for
    /// <see cref="ChatClientAgent.CreateSessionAsync"/> which is not
    /// available on the base <see cref="AIAgent"/> type.
    /// </summary>
    private ChatClientAgent? _rawOrchestrator;
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
        - **personal-finance-agent**: Manages personal financial accounts, transactions, bills,
          and spending insights. Use this for personal finance management, budgeting questions,
          spending analysis, bill tracking, and account management.
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

        Human-in-the-Loop Approval:
        When the user requests an action that creates, modifies, or deletes data (e.g.,
        creating an invoice, issuing a payment, cancelling an order, modifying a ledger
        entry), you MUST first call the `confirmAction` tool to obtain explicit user
        approval BEFORE invoking the domain agent to execute the mutation. The
        `confirmAction` tool presents the user with an approval card showing the action
        details and Approve/Reject buttons. Only proceed with the mutating domain agent
        call if the user approves. If the user rejects, inform them that the action was
        cancelled. Read-only queries (listing, searching, viewing details) do NOT require
        approval — only mutations do.
        """;

    public MasterOrchestratorService(
        IEnumerable<IDomainAgentDescriptor> descriptors,
        IMcpToolProvider mcpToolProvider,
        IAgentConfigurationService configService,
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        ICurrentUserProvider currentUserProvider,
        IConfiguration configuration,
        ILogger<MasterOrchestratorService> logger)
    {
        _descriptors = descriptors;
        _mcpToolProvider = mcpToolProvider;
        _configService = configService;
        _chatClient = chatClient;
        _serviceProvider = serviceProvider;
        _currentUserProvider = currentUserProvider;
        _logger = logger;
        _enableSensitiveData = configuration.GetValue<bool>(AiTelemetry.EnableSensitiveDataKey);
    }

    public async Task<AgentChatResponse> ChatAsync(
        ChatRequest request,
        CancellationToken cancellationToken = default)
    {
        var sessionId = request.SessionId ?? Guid.NewGuid().ToString("N");

        // Propagate session and user identifiers as OTel baggage + span attributes.
        // The BaggageSpanProcessor in ServiceDefaults copies these to all child spans,
        // enabling Langfuse session grouping and user attribution.
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetBaggage(AiTelemetry.SessionIdAttribute, sessionId);
            activity.SetTag(AiTelemetry.SessionIdAttribute, sessionId);

            if (_currentUserProvider.TryGetCurrentUserId(out var userId))
            {
                var userIdStr = userId.ToString();
                activity.SetBaggage(AiTelemetry.UserIdAttribute, userIdStr);
                activity.SetTag(AiTelemetry.UserIdAttribute, userIdStr);
            }
        }

        _logger.LogInformation(
            "Orchestrator processing message for session {SessionId}",
            sessionId);

        // Ensure the orchestrator agent is built and cached
        var orchestrator = await GetOrBuildOrchestratorAsync(cancellationToken);

        // Get or create MAF session for this conversation.
        // CreateSessionAsync produces a ChatClientAgentSession that tracks
        // conversation history natively — no manual message list management needed.
        var session = await GetOrCreateSessionAsync(sessionId, cancellationToken);

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

    /// <inheritdoc />
    public async Task<AIAgent> GetAgentAsync(CancellationToken cancellationToken = default)
    {
        return await GetOrBuildOrchestratorAsync(cancellationToken);
    }

    /// <summary>
    /// Gets or creates a MAF session for the given session ID.
    /// Uses <see cref="ChatClientAgent.CreateSessionAsync"/> to create sessions
    /// that natively track conversation history.
    /// </summary>
    private async Task<AgentSession> GetOrCreateSessionAsync(
        string sessionId,
        CancellationToken cancellationToken)
    {
        if (Sessions.TryGetValue(sessionId, out var existing))
            return existing;

        // Create a new session via the raw ChatClientAgent (which exposes
        // CreateSessionAsync). This returns a ChatClientAgentSession that
        // tracks messages internally across RunAsync calls.
        var session = await _rawOrchestrator!.CreateSessionAsync(
            sessionId, cancellationToken);

        // Cache it. If another thread raced us, use the winner's session.
        return Sessions.GetOrAdd(sessionId, session);
    }

    /// <summary>
    /// Returns the cached orchestrator agent, building it on first call.
    /// The agent is stateless and can be safely reused across requests;
    /// per-conversation state lives in <see cref="AgentSession"/>.
    /// </summary>
    private async Task<AIAgent> GetOrBuildOrchestratorAsync(
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

            _rawOrchestrator = new ChatClientAgent(
                _chatClient,
                name: "master-orchestrator",
                instructions: OrchestratorInstructions,
                tools: tools);

            // Wrap with OpenTelemetry instrumentation to emit invoke_agent
            // spans per the GenAI semantic conventions.
            _cachedOrchestrator = _rawOrchestrator
                .AsBuilder()
                .UseOpenTelemetry(
                    AiTelemetry.SourceName,
                    configure: cfg => cfg.EnableSensitiveData = _enableSensitiveData)
                .Build();

            return _cachedOrchestrator;
        }
        finally
        {
            _buildLock.Release();
        }
    }

    /// <summary>
    /// Builds the complete tool set for the orchestrator: domain agents-as-tools
    /// plus any MCP-provided tools. For each descriptor, checks the agent
    /// configuration service for tenant/global overrides (instructions, tool set,
    /// active flag) and applies them via the overloaded
    /// <see cref="IDomainAgentDescriptor.Build(IChatClient, IServiceProvider, string?, IReadOnlySet{string}?)"/>.
    /// </summary>
    private async Task<List<AITool>> BuildAllToolsAsync(CancellationToken cancellationToken)
    {
        var tools = new List<AITool>();

        // Build domain agents as tools, applying any configuration overrides
        foreach (var descriptor in _descriptors)
        {
            try
            {
                // Check for a resolved configuration (tenant override → global default)
                var config = await _configService.GetResolvedAsync(descriptor.Name, cancellationToken);

                // If a configuration exists and the agent is inactive, skip it
                if (config is { IsActive: false })
                {
                    _logger.LogInformation(
                        "Skipping inactive agent '{AgentName}' per configuration",
                        descriptor.Name);
                    continue;
                }

                AIAgent builtAgent;

                if (config is not null)
                {
                    // Apply overrides: instructions and tool set filtering
                    var instructionsOverride = !string.IsNullOrWhiteSpace(config.InstructionsText)
                        ? config.InstructionsText
                        : null;

                    HashSet<string>? allowedToolNames = null;
                    if (!string.IsNullOrWhiteSpace(config.ToolsetIdsJson)
                        && config.ToolsetIdsJson != "[]")
                    {
                        try
                        {
                            var toolNames = JsonSerializer.Deserialize<List<string>>(config.ToolsetIdsJson);
                            if (toolNames is { Count: > 0 })
                                allowedToolNames = new HashSet<string>(toolNames, StringComparer.Ordinal);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Invalid ToolsetIdsJson for agent '{AgentName}' — using all tools",
                                descriptor.Name);
                        }
                    }

                    builtAgent = descriptor.Build(
                        _chatClient,
                        _serviceProvider,
                        instructionsOverride,
                        allowedToolNames);

                    _logger.LogDebug(
                        "Built domain agent tool: {AgentName} with config override (IsOverride={IsOverride})",
                        descriptor.Name, config.IsOverride);
                }
                else
                {
                    // No configuration in database — use code-based defaults
                    builtAgent = descriptor.Build(_chatClient, _serviceProvider);

                    _logger.LogDebug(
                        "Built domain agent tool: {AgentName} — {Description}",
                        descriptor.Name, descriptor.Description);
                }

                // Wrap the domain agent with OpenTelemetry instrumentation to emit
                // invoke_agent <agent-name> spans per the GenAI semantic conventions.
                var instrumentedAgent = builtAgent
                    .AsBuilder()
                    .UseOpenTelemetry(
                        AiTelemetry.SourceName,
                        configure: cfg => cfg.EnableSensitiveData = _enableSensitiveData)
                    .Build();

                var agentTool = instrumentedAgent.AsAIFunction();
                tools.Add(agentTool);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to build domain agent '{AgentName}' as tool",
                    descriptor.Name);
            }
        }

        // Wire in MCP tools
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
