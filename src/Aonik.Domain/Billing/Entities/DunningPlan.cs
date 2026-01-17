using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Billing.Entities;

public class DunningPlan : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CustomerAccountId { get; set; }
    public string PolicyJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
