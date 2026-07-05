using Aonik.SharedKernel.Primitives;
using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Entities;

public class FinancialLifeGraphNode : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? SourceEntity { get; set; }
    public Guid? SourceId { get; set; }
    public string PropertiesJson { get; set; } = "{}";
    public FinancialLifeGraphEntityStatus Status { get; set; }
    public bool IsInferred { get; set; }
    public Guid? AiRunId { get; set; }
}
