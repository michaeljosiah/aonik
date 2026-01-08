using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Pricing.Entities;

public class FeePolicy : AuditableEntity
{
    public Guid FeePolicyId { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal FixedFee { get; private set; }
    public decimal PercentageFee { get; private set; }
    public string ConditionsJson { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private FeePolicy() { }

    public FeePolicy(Guid tenantId, string name, decimal fixedFee, decimal percentageFee)
    {
        FeePolicyId = Id;
        TenantId = tenantId;
        Name = name;
        FixedFee = fixedFee;
        PercentageFee = percentageFee;
        ConditionsJson = "{}";
        IsActive = true;
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateFees(decimal fixedFee, decimal percentageFee)
    {
        FixedFee = fixedFee;
        PercentageFee = percentageFee;
    }

    public void UpdateConditions(string conditionsJson)
    {
        ConditionsJson = conditionsJson;
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
