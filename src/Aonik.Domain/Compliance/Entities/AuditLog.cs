using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class AuditLog : AuditableEntity
{
    public Guid AuditLogId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string ResourceType { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }
    public string DetailsJson { get; private set; } = string.Empty;

    private AuditLog() { }

    public AuditLog(Guid tenantId, string actorType, Guid actorId, string action, string resourceType, Guid resourceId, string detailsJson = "{}")
    {
        AuditLogId = Id;
        TenantId = tenantId;
        Timestamp = DateTime.UtcNow;
        ActorType = actorType;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        DetailsJson = detailsJson;
    }
}
