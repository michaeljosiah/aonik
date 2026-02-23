using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Notifications;

public class NotificationTemplateBinding : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public Guid? BaseTemplateId { get; set; }
    public Guid? OverrideTemplateId { get; set; }
    public bool IsEnabled { get; set; } = true;
}
