using System.Text.Json;

namespace Aonik.Agents.Contracts.Models;

/// <summary>
/// Request DTO for the AI Playground streaming endpoint.
/// Supports two modes:
/// <list type="bullet">
///   <item><b>Agent mode</b> — set <see cref="AgentName"/> to resolve a registered
///     <c>IDomainAgentDescriptor</c>, pre-loading its instructions and tools.
///     <see cref="SystemPrompt"/> and <see cref="EnabledToolNames"/> act as overrides.</item>
///   <item><b>Raw mode</b> — omit <see cref="AgentName"/> and provide a
///     <see cref="SystemPrompt"/> directly. No agent tools are loaded.</item>
/// </list>
/// </summary>
public sealed record PlaygroundRunRequest
{
    /// <summary>
    /// Agent descriptor name (e.g. "personal-finance-agent"). When set, the
    /// endpoint resolves the descriptor and builds the agent with its tools.
    /// When <c>null</c>, raw mode is used.
    /// </summary>
    public string? AgentName { get; init; }

    /// <summary>
    /// System prompt text. In agent mode this overrides the descriptor's
    /// instructions; in raw mode this is the only system prompt.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// AI model ID (GUID) to use for this run. Overrides the model configured
    /// on the agent or route policy. When <c>null</c>, the default model is used.
    /// </summary>
    public Guid? ModelId { get; init; }

    /// <summary>
    /// Optional User Brief JSON to inject as a system message before the
    /// conversation. Can be a real projected brief or a sample/manual payload.
    /// </summary>
    public string? UserBriefJson { get; init; }

    /// <summary>
    /// Optional user to impersonate for the duration of this playground run.
    /// When set, the request-scoped <see cref="Aonik.SharedKernel.Abstractions.ICurrentUserContext"/>
    /// has its <c>UserId</c> overridden to this value early in the handler, so
    /// every downstream service / agent tool that resolves the current user
    /// (e.g. <c>BillService</c>, <c>DashboardService</c>, the personal-finance
    /// sub-agents' read tools) targets the impersonated user's data instead of
    /// the authenticated admin's. Gated by <c>AdminUserPolicy</c> on the
    /// endpoint — only admins can impersonate. Tenancy is not affected; the
    /// impersonated user must belong to the current tenant.
    /// </summary>
    public Guid? ImpersonateUserId { get; init; }

    /// <summary>
    /// Tool filter for agent mode. Only tools whose names appear in this list
    /// will be available to the LLM. When <c>null</c>, all agent tools are used.
    /// Ignored in raw mode.
    /// </summary>
    public List<string>? EnabledToolNames { get; init; }

    /// <summary>
    /// Conversation messages. Typically a single user message, but supports
    /// multi-turn history for iterative testing.
    /// </summary>
    public List<PlaygroundMessage>? Messages { get; init; }

    /// <summary>LLM temperature (0.0 – 2.0). When <c>null</c>, uses model default.</summary>
    public float? Temperature { get; init; }

    /// <summary>Maximum tokens to generate. When <c>null</c>, uses model default.</summary>
    public int? MaxTokens { get; init; }

    /// <summary>
    /// Optional client-side tool declarations that should be exposed to the LLM
    /// during playground runs. These mirror the AG-UI frontend tool contract and
    /// are emitted as function calls for the Admin UI to execute locally.
    /// </summary>
    public List<JsonElement>? ToolDefinitions { get; init; }

    // ── AI Task mode ────────────────────────────────────────────────────

    /// <summary>
    /// AI Task ID (GUID) to test in the playground. When set, the endpoint
    /// resolves the task's system and user prompt templates, applies variable
    /// substitution from <see cref="PromptVariables"/>, and runs the LLM
    /// directly (no agent). Takes precedence over <see cref="AgentName"/>.
    /// </summary>
    public Guid? AiTaskId { get; init; }

    /// <summary>
    /// Key-value pairs to substitute into the AI Task's prompt templates.
    /// Template variables use <c>{{variableName}}</c> syntax.
    /// Only used when <see cref="AiTaskId"/> is set.
    /// </summary>
    public Dictionary<string, string>? PromptVariables { get; init; }
}

/// <summary>
/// Simplified message DTO for playground conversations.
/// </summary>
public sealed record PlaygroundMessage
{
    public string? Id { get; init; }
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
    public string? ToolCallId { get; init; }
    public string? Name { get; init; }
    public List<PlaygroundToolCall>? ToolCalls { get; init; }
}

public sealed record PlaygroundToolCall
{
    public string Id { get; init; } = string.Empty;
    public string Type { get; init; } = "function";
    public PlaygroundFunctionCall? Function { get; init; }
}

public sealed record PlaygroundFunctionCall
{
    public string Name { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
}

/// <summary>
/// Token usage and performance metrics returned in the <c>RUN_FINISHED</c> SSE event.
/// </summary>
public sealed record PlaygroundRunMetrics
{
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public long TotalTokens { get; init; }
    public long LatencyMs { get; init; }
    public decimal? EstimatedCostUsd { get; init; }
}
