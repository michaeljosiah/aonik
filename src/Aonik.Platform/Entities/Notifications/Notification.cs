using Aonik.SharedKernel.Primitives;
using Aonik.Platform.Notifications;

namespace Aonik.Platform.Entities.Notifications;

public class Notification : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Channel { get; set; } = NotificationChannels.InApp;
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Severity { get; set; } = NotificationSeverities.Info;
    public string Status { get; set; } = NotificationStatuses.Unread;
    public string? ActionUrl { get; set; }
    public string? CorrelationId { get; set; }

    /// <summary>
    /// Optional caller-supplied deduplication key. When set, a unique index on
    /// <c>(TenantId, UserId, IdempotencyKey)</c> guarantees at most one notification per key, so a
    /// producer can make creation idempotent (e.g. the task scheduler keys each occurrence's reminder
    /// on its run id). Null for the common case, where no uniqueness is enforced.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public Guid? AiRunId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}
