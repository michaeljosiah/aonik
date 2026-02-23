using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Compliance;

public class AuditLog : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public DateTime Timestamp { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
}
