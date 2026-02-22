namespace Aonik.SharedKernel.Events;

/// <summary>
/// Marker interface for integration events that cross module boundaries.
/// All events published through <see cref="IEventBus"/> must implement this interface.
/// Use records for immutability: <c>public record OrderCreatedEvent(Guid OrderId) : IIntegrationEvent;</c>
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event instance, used for idempotency and tracing.
    /// </summary>
    Guid EventId => Guid.NewGuid();

    /// <summary>
    /// UTC timestamp when the event was created.
    /// </summary>
    DateTime OccurredAt => DateTime.UtcNow;
}
