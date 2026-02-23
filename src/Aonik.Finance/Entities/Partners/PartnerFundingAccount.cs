using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Partners;

public class PartnerFundingAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PartnerId { get; set; }
    public Guid LedgerAccountId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string AccountRole { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
