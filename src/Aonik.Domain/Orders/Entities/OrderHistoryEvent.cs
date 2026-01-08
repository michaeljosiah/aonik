using Aonik.SharedKernel.Primitives;

namespace Aonik.Domain.Orders.Entities;

public class OrderHistoryEvent : AuditableEntity
{
    public Guid OrderHistoryEventId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrderId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTime EventAt { get; private set; }
    public string ActorType { get; private set; } = string.Empty;
    public Guid ActorId { get; private set; }
    public string DetailsJson { get; private set; } = string.Empty;

    private OrderHistoryEvent() { }

    public OrderHistoryEvent(Guid tenantId, Guid orderId, string eventType, string actorType, Guid actorId, string detailsJson = "{}")
    {
        OrderHistoryEventId = Id;
        TenantId = tenantId;
        OrderId = orderId;
        EventType = eventType;
        EventAt = DateTime.UtcNow;
        ActorType = actorType;
        ActorId = actorId;
        DetailsJson = detailsJson;
    }
}
