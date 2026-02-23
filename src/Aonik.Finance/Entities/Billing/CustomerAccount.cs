using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Billing;

public class CustomerAccount : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid MerchantPartyId { get; set; }
    public Guid CustomerPartyId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PreferencesJson { get; set; } = string.Empty;
}
