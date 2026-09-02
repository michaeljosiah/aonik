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

        // Spec 097 §12.3: handlers are NOT module-gated, deliberately. An outbox message is work the
        // tenant already committed while the module was on; refusing to react to it corrupts state
        // rather than protecting it. Skipping (and recording) a usage-drawdown handler because
        // Subscriptions was switched off between the commit and the drain would permanently lose the
        // revenue-recognition and provider-cost journal entries, leaving entitlement state
        // inconsistent with the ledger for good — the ledger is the source of financial truth, so a
        // reaction to committed work must complete. The module gate belongs at the ENTRY points
        // (HTTP, agents, jobs, proposals), which is where new activity is refused.
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
