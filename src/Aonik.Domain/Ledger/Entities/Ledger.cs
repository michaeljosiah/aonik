using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class Ledger : AuditableEntity, ITenantScoped
{
    public Guid LedgerId { get; private set; }
    public Guid TenantId { get; private set; }
    public string BaseCurrency { get; private set; } = string.Empty;

    private readonly List<LedgerAccount> _accounts = new();
    public IReadOnlyCollection<LedgerAccount> Accounts => _accounts.AsReadOnly();

    private Ledger() { }

    public Ledger(Guid tenantId, string baseCurrency)
    {
        LedgerId = Id;
        TenantId = tenantId;
        BaseCurrency = baseCurrency;
    }

    public void UpdateBaseCurrency(string baseCurrency)
    {
        BaseCurrency = baseCurrency;
    }

    public void AddAccount(LedgerAccount account)
    {
        _accounts.Add(account);
    }
}
