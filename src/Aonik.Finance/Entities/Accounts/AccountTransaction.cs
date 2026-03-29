using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Accounts;

public class AccountTransaction : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? AccountConnectionId { get; set; }
    public string ProviderTransactionReference { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Counterparty { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public string? Category { get; set; }
    public bool Pending { get; set; }
    public string ReconciliationStatus { get; set; } = string.Empty;
    public Guid? MatchedLedgerEntryId { get; set; }
    public Guid? MatchedPayoutId { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public Guid? ReconciledByUserId { get; set; }
    public string? Notes { get; set; }
}
