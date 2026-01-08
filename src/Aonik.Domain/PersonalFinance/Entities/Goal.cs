using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class Goal : AuditableEntity, ITenantScoped
{
    public Guid GoalId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal TargetAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTime? TargetDate { get; private set; }
    public decimal ProgressAmount { get; private set; }
    public string Status { get; private set; } = string.Empty;

    private Goal() { }

    public Goal(Guid tenantId, Guid userId, string name, decimal targetAmount, string currency, DateTime? targetDate = null)
    {
        GoalId = Id;
        TenantId = tenantId;
        UserId = userId;
        Name = name;
        TargetAmount = targetAmount;
        Currency = currency;
        TargetDate = targetDate;
        ProgressAmount = 0;
        Status = "Active";
    }

    public void UpdateName(string name)
    {
        Name = name;
    }

    public void UpdateTarget(decimal targetAmount, DateTime? targetDate = null)
    {
        TargetAmount = targetAmount;
        if (targetDate.HasValue)
        {
            TargetDate = targetDate;
        }
    }

    public void RecordProgress(decimal amount)
    {
        ProgressAmount += amount;
        
        if (ProgressAmount >= TargetAmount)
        {
            Status = "Achieved";
        }
    }

    public void UpdateStatus(string status)
    {
        Status = status;
    }
}
