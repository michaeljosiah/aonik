using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Partners.Entities;

public class RoutingRule : AuditableEntity, ITenantScoped
{
    public Guid RoutingRuleId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ConditionsJson { get; private set; } = string.Empty;
    public Guid? TargetPartnerId { get; private set; }
    public Guid? TargetConnectorId { get; private set; }
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    private RoutingRule() { }

    public RoutingRule(Guid tenantId, string conditionsJson, int priority, Guid? targetPartnerId = null, Guid? targetConnectorId = null)
    {
        RoutingRuleId = Id;
        TenantId = tenantId;
        ConditionsJson = conditionsJson;
        TargetPartnerId = targetPartnerId;
        TargetConnectorId = targetConnectorId;
        Priority = priority;
        IsActive = true;
    }

    public void UpdateConditions(string conditionsJson)
    {
        ConditionsJson = conditionsJson;
    }

    public void UpdateTarget(Guid? targetPartnerId, Guid? targetConnectorId)
    {
        TargetPartnerId = targetPartnerId;
        TargetConnectorId = targetConnectorId;
    }

    public void UpdatePriority(int priority)
    {
        Priority = priority;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
