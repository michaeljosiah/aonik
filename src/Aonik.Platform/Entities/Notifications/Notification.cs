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
    public Guid? AiRunId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public DateTime? ReadAt { get; set; }
    public DateTime? DismissedAt { get; set; }
}
