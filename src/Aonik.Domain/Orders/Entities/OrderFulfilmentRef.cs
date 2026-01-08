using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderFulfilmentRef : AuditableEntity, ITenantScoped
{
    public Guid OrderFulfilmentRefId { get; set; }
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PayoutId { get; set; }
}
