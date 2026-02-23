using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

public class RoutingRule : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ConditionsJson { get; set; } = string.Empty;
    public Guid? TargetPartnerId { get; set; }
    public Guid? TargetConnectorId { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; }
}
