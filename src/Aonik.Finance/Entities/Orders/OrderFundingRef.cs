using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

public class OrderFundingRef : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PaymentIntentId { get; set; }
}
