using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A single message within a <see cref="ChatThread"/>. Persists both user and
/// assistant messages to enable server-side history loading without client replay.
/// </summary>
public class ChatThreadMessage : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ChatThreadId { get; set; }

    /// <summary>
    /// Message role: "user", "assistant", "system", or "tool".
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// The text content of the message.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Name of the agent that produced this message (for assistant messages).
    /// </summary>
    public string? AgentName { get; set; }

    /// <summary>
    /// Optional reference to the AI run that produced this message.
    /// </summary>
    public Guid? AiRunId { get; set; }

    /// <summary>
    /// JSON-serialised tool call information (for assistant messages with tool calls).
    /// </summary>
    public string? ToolCallsJson { get; set; }

    /// <summary>
    /// Ordering within the thread (monotonically increasing per thread).
    /// </summary>
    public int SortOrder { get; set; }

    public ChatThread ChatThread { get; set; } = null!;
}
