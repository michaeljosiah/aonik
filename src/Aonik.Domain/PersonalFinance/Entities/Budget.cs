using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class Budget : AuditableEntity
{
    public Guid BudgetId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string PeriodType { get; private set; } = string.Empty;
    public DateTime PeriodStart { get; private set; }
    public string BudgetCreatedBy { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;

    private readonly List<BudgetLine> _lines = new();
    public IReadOnlyCollection<BudgetLine> Lines => _lines.AsReadOnly();

    private Budget() { }

    public Budget(Guid tenantId, Guid userId, string periodType, DateTime periodStart, string budgetCreatedBy)
    {
        BudgetId = Id;
        TenantId = tenantId;
        UserId = userId;
        PeriodType = periodType;
        PeriodStart = periodStart;
        BudgetCreatedBy = budgetCreatedBy;
        Status = "Active";
    }

    public void AddLine(BudgetLine line)
    {
        _lines.Add(line);
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }

    public void Close()
    {
        Status = "Closed";
    }
}
