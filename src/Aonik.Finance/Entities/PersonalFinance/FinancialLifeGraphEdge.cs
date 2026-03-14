using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class FinancialLifeGraphEdge : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string FromNodeKey { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string ToNodeKey { get; set; } = string.Empty;
    public string PropertiesJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public bool IsInferred { get; set; }
    public Guid? AiRunId { get; set; }
}
