using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Notifications;

public class NotificationDevice : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string DeviceToken { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime LastSeenAtUtc { get; set; }
    public DateTime? InvalidatedAtUtc { get; set; }
    public string? LastError { get; set; }
}
