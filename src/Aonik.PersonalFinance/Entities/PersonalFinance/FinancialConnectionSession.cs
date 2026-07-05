using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

public class FinancialConnectionSession : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? FinancialConnectionId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string? ProviderSessionReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
