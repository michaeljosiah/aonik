using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class FinancialLinkedAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid FinancialConnectionId { get; set; }
    public Guid PersonalAccountId { get; set; }
    public string ProviderAccountReference { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountSubtype { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Last4 { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastError { get; set; }
}
