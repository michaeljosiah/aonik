using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class BalanceSnapshot : AuditableEntity, ITenantScoped
{
    public Guid BalanceSnapshotId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LedgerAccountId { get; private set; }
    public DateTime AsOf { get; private set; }
    public decimal Balance { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    private BalanceSnapshot() { }

    public BalanceSnapshot(Guid tenantId, Guid ledgerAccountId, DateTime asOf, decimal balance, string currency)
    {
        BalanceSnapshotId = Id;
        TenantId = tenantId;
        LedgerAccountId = ledgerAccountId;
        AsOf = asOf;
        Balance = balance;
        Currency = currency;
    }
}
