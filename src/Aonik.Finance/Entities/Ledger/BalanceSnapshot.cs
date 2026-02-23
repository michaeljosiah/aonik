using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Ledger;

public class BalanceSnapshot : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public DateTime AsOf { get; set; }
    public decimal Balance { get; set; }
    public string Currency { get; set; } = string.Empty;
}
