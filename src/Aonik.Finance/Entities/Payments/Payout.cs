using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Payments;

public class Payout : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public Guid? DestinationExternalAccountId { get; set; }
    public Guid? PartnerId { get; set; }
    public string Status { get; set; } = string.Empty;
}
