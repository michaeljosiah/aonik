using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.SharedKernel.Events;

/// <summary>
/// In-process event bus that resolves handlers from the DI container.
/// Handlers execute sequentially within the current scope (shares DbContext transaction).
/// Register with <c>services.AddEventBus()</c>.
/// </summary>
public class InProcessEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InProcessEventBus> _logger;

    public InProcessEventBus(IServiceProvider serviceProvider, ILogger<InProcessEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        var eventType = typeof(TEvent).Name;
        _logger.LogDebug("Publishing event {EventType}", eventType);

        var handlers = _serviceProvider.GetServices<IEventHandler<TEvent>>();
        var handlerList = handlers.ToList();

        if (handlerList.Count == 0)
        {
            _logger.LogDebug("No handlers registered for {EventType}", eventType);
            return;
        }

        _logger.LogDebug("Found {HandlerCount} handler(s) for {EventType}", handlerList.Count, eventType);

        foreach (var handler in handlerList)
        {
            var handlerType = handler.GetType().Name;
            try
            {
                _logger.LogDebug("Invoking handler {HandlerType} for {EventType}", handlerType, eventType);
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler {HandlerType} failed for event {EventType}", handlerType, eventType);
                throw;
            }
        }
    }
}
