using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class BudgetLine : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid BudgetId { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal LimitAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
}
