using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class JournalEntryLine : AuditableEntity
{
    public Guid JournalEntryLineId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid JournalEntryId { get; private set; }
    public Guid LedgerAccountId { get; private set; }
    public string Direction { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public string? Narration { get; private set; }
    public string DimensionsJson { get; private set; } = string.Empty;

    private JournalEntryLine() { }

    public JournalEntryLine(Guid tenantId, Guid journalEntryId, Guid ledgerAccountId, string direction, decimal amount, string currency, string? narration = null)
    {
        JournalEntryLineId = Id;
        TenantId = tenantId;
        JournalEntryId = journalEntryId;
        LedgerAccountId = ledgerAccountId;
        Direction = direction;
        Amount = amount;
        Currency = currency;
        Narration = narration;
        DimensionsJson = "{}";
    }

    public void UpdateNarration(string? narration)
    {
        Narration = narration;
    }

    public void UpdateDimensions(string dimensionsJson)
    {
        DimensionsJson = dimensionsJson;
    }
}
