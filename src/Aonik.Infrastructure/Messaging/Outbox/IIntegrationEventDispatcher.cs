using Aonik.SharedKernel.Events.Outbox;

namespace Aonik.Infrastructure.Messaging.Outbox;

/// <summary>
/// Rehydrates and dispatches a single <see cref="OutboxMessage"/> to every
/// registered <see cref="Aonik.SharedKernel.Events.IEventHandler{TEvent}"/>,
/// recording per-handler idempotency in the inbox. Resolved within the same DI
/// scope as the processor so its inbox writes share the processor's DbContext and
/// commit together with the message status.
/// </summary>
public interface IIntegrationEventDispatcher
{
    /// <summary>
    /// Resolves the event type, deserializes the payload, and invokes each handler
    /// that has not already processed this (event, handler) pair. Inbox rows for
    /// successful handlers are staged on the scoped DbContext but NOT saved here —
    /// the caller commits them alongside the message status. If a handler throws,
    /// the exception propagates AFTER inbox rows for the handlers that already
    /// succeeded have been staged, so a retry skips them.
    /// </summary>
    Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
