using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class Ledger : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string BaseCurrency { get; set; } = string.Empty;
    public List<LedgerAccount> Accounts { get; set; } = new();
}
