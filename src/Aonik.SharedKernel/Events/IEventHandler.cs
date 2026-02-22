namespace Aonik.SharedKernel.Events;

/// <summary>
/// Handles a specific type of integration event.
/// Implementations are discovered and registered automatically by <c>AddEventBus()</c>.
/// </summary>
/// <typeparam name="TEvent">The integration event type to handle.</typeparam>
public interface IEventHandler<in TEvent> where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Handles the event. Exceptions will propagate to the publisher.
    /// </summary>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
