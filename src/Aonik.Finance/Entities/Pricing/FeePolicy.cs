using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Pricing;

public class FeePolicy : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal FixedFee { get; set; }
    public decimal PercentageFee { get; set; }
    public string ConditionsJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
