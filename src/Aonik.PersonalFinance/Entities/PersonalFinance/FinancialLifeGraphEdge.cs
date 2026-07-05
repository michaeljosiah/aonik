using Aonik.SharedKernel.Primitives;
using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Entities;

public class FinancialLifeGraphEdge : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string FromNodeKey { get; set; } = string.Empty;
    public string Predicate { get; set; } = string.Empty;
    public string ToNodeKey { get; set; } = string.Empty;
    public string PropertiesJson { get; set; } = "{}";
    public FinancialLifeGraphEntityStatus Status { get; set; }
    public bool IsInferred { get; set; }
    public Guid? AiRunId { get; set; }
}
