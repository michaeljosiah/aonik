using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class Goal : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? TargetDate { get; set; }
    public decimal ProgressAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}
