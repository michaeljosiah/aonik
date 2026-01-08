using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Notifications.Entities;

public class Notification : AuditableEntity, ITenantScoped
{
    public Guid NotificationId { get; set; }
    public Guid TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public string RecipientRef { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
}
