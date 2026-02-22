namespace Aonik.SharedKernel.Events;

/// <summary>
/// Publishes integration events to all registered handlers.
/// In the modular monolith, this is an in-process bus.
/// Can be replaced with a distributed bus (e.g., MassTransit, Azure Service Bus) later.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an event to all registered <see cref="IEventHandler{TEvent}"/> implementations.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent;
}
