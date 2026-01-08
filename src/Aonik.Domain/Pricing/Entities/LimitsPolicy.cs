using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Pricing.Entities;

public class LimitsPolicy : AuditableEntity
{
    public Guid LimitsPolicyId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ScopeType { get; private set; } = string.Empty;
    public Guid? ScopeId { get; private set; }
    public decimal MaxAmount { get; private set; }
    public string Period { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private LimitsPolicy() { }

    public LimitsPolicy(Guid tenantId, string scopeType, decimal maxAmount, string period, string currency, Guid? scopeId = null)
    {
        LimitsPolicyId = Id;
        TenantId = tenantId;
        ScopeType = scopeType;
        ScopeId = scopeId;
        MaxAmount = maxAmount;
        Period = period;
        Currency = currency;
        IsActive = true;
    }

    public void UpdateMaxAmount(decimal maxAmount)
    {
        MaxAmount = maxAmount;
    }

    public void UpdatePeriod(string period)
    {
        Period = period;
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
