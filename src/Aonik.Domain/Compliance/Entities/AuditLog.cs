using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Compliance.Entities;

public class AuditLog : AuditableEntity, ITenantScoped
{
    public Guid AuditLogId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
}
