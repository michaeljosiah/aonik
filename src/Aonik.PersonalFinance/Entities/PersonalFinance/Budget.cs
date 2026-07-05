using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class Budget : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string PeriodType { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public string BudgetCreatedBy { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<BudgetLine> Lines { get; set; } = new();
}
