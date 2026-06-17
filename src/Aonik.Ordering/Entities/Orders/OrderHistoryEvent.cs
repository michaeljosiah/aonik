using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Orders;

public class OrderHistoryEvent : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid OrderId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventAt { get; set; }
    public string ActorType { get; set; } = string.Empty;
    public Guid ActorId { get; set; }
    public string DetailsJson { get; set; } = string.Empty;
}
