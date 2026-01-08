using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.PersonalFinance.Entities;

public class CategorisationRule : AuditableEntity, ITenantScoped
{
    public Guid CategorisationRuleId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Pattern { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public int Priority { get; private set; }
    public bool IsActive { get; private set; }

    private CategorisationRule() { }

    public CategorisationRule(Guid tenantId, Guid userId, string pattern, string category, int priority)
    {
        CategorisationRuleId = Id;
        TenantId = tenantId;
        UserId = userId;
        Pattern = pattern;
        Category = category;
        Priority = priority;
        IsActive = true;
    }

    public void UpdatePattern(string pattern)
    {
        Pattern = pattern;
    }

    public void UpdateCategory(string category)
    {
        Category = category;
    }

    public void UpdatePriority(int priority)
    {
        Priority = priority;
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
