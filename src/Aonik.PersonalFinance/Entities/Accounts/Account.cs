using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities.Accounts;

public class Account : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? AccountSubtype { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string? MaskedIdentifier { get; set; }
    public string? InstitutionName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public Guid? AccountConnectionId { get; set; }
    public string? ProviderAccountReference { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastError { get; set; }
}
