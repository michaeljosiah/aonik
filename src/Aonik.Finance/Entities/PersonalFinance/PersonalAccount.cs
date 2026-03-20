using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class PersonalAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? HouseholdId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string? InstitutionName { get; set; }
    public string? ExternalReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AccountSubtype { get; set; }
    public string? Last4 { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime? BalanceAsOf { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
}
