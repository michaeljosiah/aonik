using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderFulfilmentRef : AuditableEntity, ITenantScoped
{
    public Guid OrderFulfilmentRefId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid PayoutId { get; private set; }

    private OrderFulfilmentRef() { }

    public OrderFulfilmentRef(Guid tenantId, Guid orderId, Guid payoutId)
    {
        OrderFulfilmentRefId = Id;
        TenantId = tenantId;
        OrderId = orderId;
        PayoutId = payoutId;
    }
}
