using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class BalanceSnapshot : AuditableEntity, ITenantScoped
{
    public Guid BalanceSnapshotId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public DateTime AsOf { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
}
