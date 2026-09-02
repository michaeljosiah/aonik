using Aonik.SharedKernel.Modules;
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

        // Spec 097 §12.3: resolved at most once per publish, and only when a handler is gated.
        ModuleEnablementSet? enablement = null;
        var enablementResolved = false;

        foreach (var handler in handlerList)
        {
            var handlerType = handler.GetType().Name;

            var gatedModuleId = TenantScopedHandlerGate.GatedModuleId(handler.GetType());
            if (gatedModuleId is not null)
            {
                if (!enablementResolved)
                {
                    enablement = await TenantScopedHandlerGate.TryResolveAsync(_serviceProvider, @event, cancellationToken);
                    enablementResolved = true;
                }

                if (enablement is not null && !enablement.IsEnabled(gatedModuleId))
                {
                    _logger.LogDebug(
                        "Skipping handler {HandlerType} for {EventType}: module '{ModuleId}' is disabled for tenant {TenantId}",
                        handlerType, eventType, gatedModuleId, enablement.TenantId);
                    continue;
                }
            }

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

/// <summary>
/// The module gate for integration event handlers (Spec 097 §12.3), shared by the in-process bus
/// and the outbox dispatcher. A handler is skipped when the event is <see cref="ITenantScopedEvent"/>
/// and the handler's assembly module is a known, non-core module that is disabled for that tenant.
/// Events without a tenant and handlers in core modules (or in assemblies with no module
/// attribute) are never skipped.
/// </summary>
/// <remarks>
/// The reader is resolved lazily from the scope so hosts and tests that compose the bus without
/// Platform keep working: when no <see cref="IModuleEnablementReader"/> is registered every
/// handler runs. A reader failure propagates — the gate never guesses.
/// </remarks>
public static class TenantScopedHandlerGate
{
    /// <summary>
    /// The module id that gates <paramref name="handlerType"/>, or null when the handler can never
    /// be skipped (no module attribute, unknown id, or a core module).
    /// </summary>
    public static string? GatedModuleId(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        var moduleId = ModuleCatalog.TryGetModuleId(handlerType);
        return moduleId is not null && ModuleCatalog.IsKnown(moduleId) && !ModuleCatalog.CoreIds.Contains(moduleId)
            ? moduleId
            : null;
    }

    /// <summary>
    /// Resolves the enablement set for the event's tenant, or null when the event is not
    /// tenant-scoped, carries no tenant, or no reader is registered in <paramref name="services"/>.
    /// </summary>
    public static async Task<ModuleEnablementSet?> TryResolveAsync(
        IServiceProvider services,
        object @event,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (@event is not ITenantScopedEvent scoped || scoped.TenantId == Guid.Empty)
            return null;

        var reader = services.GetService<IModuleEnablementReader>();
        if (reader is null)
            return null;

        return await reader.GetAsync(scoped.TenantId, cancellationToken);
    }
}
