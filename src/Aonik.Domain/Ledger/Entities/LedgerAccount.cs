using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class LedgerAccount : Entity
{
    public string Name { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public DateTime CreatedUtc { get; private set; }

    private LedgerAccount() { }

    public LedgerAccount(string name, string currency)
    {
        Name = name;
        Currency = currency;
        CreatedUtc = DateTime.UtcNow;
    }
}
