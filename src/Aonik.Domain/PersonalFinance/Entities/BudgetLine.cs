using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class BudgetLine : AuditableEntity
{
    public Guid BudgetLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BudgetId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public decimal LimitAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    private BudgetLine() { }

    public BudgetLine(Guid tenantId, Guid budgetId, string category, decimal limitAmount, string currency)
    {
        BudgetLineId = Id;
        TenantId = tenantId;
        BudgetId = budgetId;
        Category = category;
        LimitAmount = limitAmount;
        Currency = currency;
    }

    public void UpdateLimit(decimal limitAmount)
    {
        LimitAmount = limitAmount;
    }
}
