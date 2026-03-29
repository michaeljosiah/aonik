using Aonik.SharedKernel.Primitives;

namespace Aonik.Agents.Entities;

/// <summary>
/// AI-generated summary of a completed chat session. References the ChatThread
/// it was derived from and captures semantic context (decisions, open loops,
/// recommendation outcomes) that AiRuns does not store.
/// </summary>
public class ConversationSummary : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>
    /// The chat thread this summary was generated from.
    /// </summary>
    public Guid ChatThreadId { get; set; }

    public DateTime SessionStartedAt { get; set; }
    public DateTime? SessionEndedAt { get; set; }

    /// <summary>
    /// AI-generated natural language summary of the session.
    /// </summary>
    public string SummaryText { get; set; } = string.Empty;

    /// <summary>
    /// JSON array of decisions: [{"decision": "...", "context": "..."}]
    /// </summary>
    public string? KeyDecisionsJson { get; set; }

    /// <summary>
    /// JSON array of unresolved items: [{"description": "...", "priority": "...", "dueDate": "..."}]
    /// </summary>
    public string? OpenLoopsJson { get; set; }

    /// <summary>
    /// JSON array: [{"recommendationId": "...", "outcome": "Accepted|Declined|Deferred", "reason": "..."}]
    /// </summary>
    public string? RecommendationOutcomesJson { get; set; }

    public DateTime CreatedAt { get; set; }

    public ChatThread ChatThread { get; set; } = null!;
}
