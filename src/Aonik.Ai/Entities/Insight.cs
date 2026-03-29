using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

public class Insight : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid? UserId { get; set; }
    public string SubjectType { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Structured evidence, confidence scores, and supporting data as JSON.
    /// </summary>
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Optional expiry for time-bound insights (e.g., behavioural patterns).
    /// Null means the insight does not expire.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime CreatedUtc { get; set; }
}
