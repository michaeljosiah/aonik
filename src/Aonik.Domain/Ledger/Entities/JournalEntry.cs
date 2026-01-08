using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class JournalEntry : AuditableEntity, ITenantScoped
{
    public Guid JournalEntryId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LedgerId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public Guid SourceId { get; private set; }
    public string Status { get; private set; } = string.Empty;

    private readonly List<JournalEntryLine> _lines = new();
    public IReadOnlyCollection<JournalEntryLine> Lines => _lines.AsReadOnly();

    private JournalEntry() { }

    public JournalEntry(Guid tenantId, Guid ledgerId, string sourceType, Guid sourceId)
    {
        JournalEntryId = Id;
        TenantId = tenantId;
        LedgerId = ledgerId;
        Timestamp = DateTime.UtcNow;
        SourceType = sourceType;
        SourceId = sourceId;
        Status = "Pending";
    }

    public void AddLine(JournalEntryLine line)
    {
        _lines.Add(line);
    }

    public void Post()
    {
        if (Status != "Pending")
            throw new InvalidOperationException("Only pending journal entries can be posted");

        if (!IsBalanced())
            throw new InvalidOperationException("Journal entry must be balanced before posting");

        Status = "Posted";
    }

    public void Reverse()
    {
        if (Status != "Posted")
            throw new InvalidOperationException("Only posted journal entries can be reversed");

        Status = "Reversed";
    }

    private bool IsBalanced()
    {
        var debits = _lines.Where(l => l.Direction == "Debit").Sum(l => l.Amount);
        var credits = _lines.Where(l => l.Direction == "Credit").Sum(l => l.Amount);
        return debits == credits;
    }
}
