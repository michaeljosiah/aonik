using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class DunningPlan : AuditableEntity
{
    public Guid DunningPlanId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CustomerAccountId { get; private set; }
    public string PolicyJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private DunningPlan() { }

    public DunningPlan(Guid tenantId, Guid customerAccountId, string policyJson)
    {
        DunningPlanId = Id;
        TenantId = tenantId;
        CustomerAccountId = customerAccountId;
        PolicyJson = policyJson;
        IsActive = true;
    }

    public void UpdatePolicy(string policyJson)
    {
        PolicyJson = policyJson;
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
