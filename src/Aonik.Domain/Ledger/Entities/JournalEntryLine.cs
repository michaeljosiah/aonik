using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class JournalEntryLine : AuditableEntity, ITenantScoped
{
    public Guid JournalEntryLineId { get; set; }
    public Guid TenantId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public string Direction { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Narration { get; set; }
    public string DimensionsJson { get; set; } = string.Empty;
}
