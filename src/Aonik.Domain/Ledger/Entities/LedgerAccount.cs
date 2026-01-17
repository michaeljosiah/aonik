using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Ledger.Entities;

public class LedgerAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid LedgerId { get; set; }
    public string AccountType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string DimensionsJson { get; set; } = string.Empty;
}
