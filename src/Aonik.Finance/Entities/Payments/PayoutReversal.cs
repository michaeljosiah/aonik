using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

/// <summary>
/// Models a recalled or auto-reversed transfer against the ledger - the payout-side analogue of
/// <see cref="Refund"/>. Tenant-scoped.
/// </summary>
public class PayoutReversal : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PayoutId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? ProviderReference { get; set; }

    /// <summary>PartnerTransactionStatus vocabulary, stored as string.</summary>
    public string Status { get; set; } = string.Empty;

    public Guid? JournalEntryId { get; set; }
}
