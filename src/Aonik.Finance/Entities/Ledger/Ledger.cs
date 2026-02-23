using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Ledger;

public class Ledger : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public List<LedgerAccount> Accounts { get; set; } = new();
}
