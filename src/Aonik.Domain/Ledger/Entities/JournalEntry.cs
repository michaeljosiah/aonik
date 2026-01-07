using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class JournalEntry : Entity
{
    public Guid AccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTime EntryUtc { get; private set; }
    public string? Reference { get; private set; }
    public string? Description { get; private set; }

    private JournalEntry() { }

    public JournalEntry(Guid accountId, decimal amount, string currency, string? reference = null, string? description = null)
    {
        AccountId = accountId;
        Amount = amount;
        Currency = currency;
        EntryUtc = DateTime.UtcNow;
        Reference = reference;
        Description = description;
    }
}
