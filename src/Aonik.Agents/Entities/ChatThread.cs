using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// A persisted conversation thread between a user and one or more agents.
/// Tracks the thread lifecycle, title (AI-summarised from the first prompt),
/// and links to the messages exchanged within the conversation.
/// </summary>
public class ChatThread : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public ChatThreadStatus Status { get; set; } = ChatThreadStatus.Active;

    /// <summary>
    /// Optional agent identifier when the thread targets a specific domain agent
    /// directly (bypassing the orchestrator). Null for orchestrator-routed threads.
    /// </summary>
    public string? AgentName { get; set; }

    public DateTime? LastMessageAt { get; set; }
    public int MessageCount { get; set; }

    /// <summary>
    /// Optional JSON metadata for extensibility (e.g., client context, tags).
    /// </summary>
    public string? MetadataJson { get; set; }

    public List<ChatThreadMessage> Messages { get; set; } = new();
}
