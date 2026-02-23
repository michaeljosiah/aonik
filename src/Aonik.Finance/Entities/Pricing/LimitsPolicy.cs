using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Pricing;

public class LimitsPolicy : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string ScopeType { get; set; } = string.Empty;
    public Guid? ScopeId { get; set; }
    public decimal MaxAmount { get; set; }
    public string Period { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
