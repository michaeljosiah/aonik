using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderFundingRef : AuditableEntity
{
    public Guid OrderFundingRefId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PaymentIntentId { get; private set; }

    private OrderFundingRef() { }

    public OrderFundingRef(Guid tenantId, Guid orderId, Guid paymentIntentId)
    {
        OrderFundingRefId = Id;
        TenantId = tenantId;
        OrderId = orderId;
        PaymentIntentId = paymentIntentId;
    }
}
