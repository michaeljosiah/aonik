using System.Reflection;
using System.Text.Json;
using Aonik.Infrastructure.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Outbox;
using Aonik.SharedKernel.Modules;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Dispatches one outbox message to its in-process handlers. Inbox idempotency is
/// keyed on the STORED <see cref="OutboxMessage.EventId"/> rather than the
/// deserialized event's <c>EventId</c>: the latter is a default interface member
/// that returns a fresh GUID on every access, so only the persisted value is stable.
/// </summary>
public sealed class IntegrationEventDispatcher : IIntegrationEventDispatcher
{
    private readonly AonikDbContext _dbContext;
    private readonly IServiceProvider _serviceProvider;
    private readonly IIntegrationEventTypeRegistry _typeRegistry;
    private readonly IClock _clock;
    private readonly ILogger<IntegrationEventDispatcher> _logger;

    public IntegrationEventDispatcher(
        AonikDbContext dbContext,
        IServiceProvider serviceProvider,
        IIntegrationEventTypeRegistry typeRegistry,
        IClock clock,
        ILogger<IntegrationEventDispatcher> logger)
    {
        _dbContext = dbContext;
        _serviceProvider = serviceProvider;
        _typeRegistry = typeRegistry;
        _clock = clock;
        _logger = logger;
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        var eventType = _typeRegistry.Resolve(message.EventType)
            ?? throw new InvalidOperationException(
                $"Unknown integration event type '{message.EventType}'. No matching IIntegrationEvent was found in the scanned assemblies.");

        var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType, OutboxSerialization.Options)
            ?? throw new InvalidOperationException(
                $"Outbox payload for event {message.EventId} ({message.EventType}) deserialized to null.");

        var handlerType = typeof(IEventHandler<>).MakeGenericType(eventType);
        var handlers = _serviceProvider.GetServices(handlerType)
            .Where(handler => handler is not null)
            .ToList();

        if (handlers.Count == 0)
        {
            // Durability without subscribers: the event was captured transactionally
            // and is now considered delivered. Handlers added later pick up new events.
            _logger.LogDebug("No handlers registered for {EventType} (event {EventId}); marking processed.",
                message.EventType, message.EventId);
            return;
        }

        var handleMethod = handlerType.GetMethod(nameof(IEventHandler<IIntegrationEvent>.HandleAsync))!;

        // Spec 097 §12.3: resolved at most once per message, and only when a handler is gated.
        ModuleEnablementSet? enablement = null;
        var enablementResolved = false;

        foreach (var handler in handlers)
        {
            var handlerName = handler!.GetType().FullName!;

            var alreadyProcessed = await _dbContext.Set<InboxMessage>()
                .AnyAsync(x => x.EventId == message.EventId && x.HandlerName == handlerName, cancellationToken);

            if (alreadyProcessed)
            {
                _logger.LogDebug("Handler {Handler} already processed event {EventId}; skipping.",
                    handlerName, message.EventId);
                continue;
            }

            var gatedModuleId = TenantScopedHandlerGate.GatedModuleId(handler.GetType());
            if (gatedModuleId is not null)
            {
                if (!enablementResolved)
                {
                    enablement = await TenantScopedHandlerGate.TryResolveAsync(_serviceProvider, integrationEvent, cancellationToken);
                    enablementResolved = true;
                }

                if (enablement is not null && !enablement.IsEnabled(gatedModuleId))
                {
                    // A skip is a completed delivery for this handler: the tenant had the module off
                    // when the event was delivered. The inbox row is recorded so a retry of the same
                    // outbox message (after a later handler fails) does not re-evaluate the gate, and
                    // the message is never left pending for a module the tenant switched off.
                    // Re-enabling the module later does not replay history through this handler.
                    _logger.LogDebug(
                        "Skipping handler {Handler} for event {EventId} ({EventType}): module '{ModuleId}' is disabled for tenant {TenantId}; marking processed.",
                        handlerName, message.EventId, message.EventType, gatedModuleId, enablement.TenantId);

                    _dbContext.Set<InboxMessage>().Add(new InboxMessage
                    {
                        EventId = message.EventId,
                        HandlerName = handlerName,
                        ProcessedAt = _clock.UtcNow,
                    });
                    continue;
                }
            }

            try
            {
                var task = (Task)handleMethod.Invoke(handler, [integrationEvent, cancellationToken])!;
                await task;
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                // Surface the real handler failure, not the reflection wrapper.
                throw ex.InnerException;
            }

            _dbContext.Set<InboxMessage>().Add(new InboxMessage
            {
                EventId = message.EventId,
                HandlerName = handlerName,
                ProcessedAt = _clock.UtcNow,
            });
        }
    }
}
