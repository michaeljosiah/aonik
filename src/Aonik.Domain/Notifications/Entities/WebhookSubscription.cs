using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Notifications.Entities;

public class WebhookSubscription : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string SubscriberName { get; set; } = string.Empty;
    public string EventTypesJson { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string? SecretRef { get; set; }
    public bool IsActive { get; set; }
}
