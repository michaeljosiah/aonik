using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Notifications;

public class NotificationTemplate : AuditableEntity
{
    public Guid? TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public bool IsActive { get; set; } = true;
}
