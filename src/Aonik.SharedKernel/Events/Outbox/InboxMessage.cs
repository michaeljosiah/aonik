using Aonik.SharedKernel.Primitives;

namespace Aonik.SharedKernel.Events.Outbox;

/// <summary>
/// Idempotency ledger recording that one handler has already processed one event.
/// A unique (<see cref="EventId"/>, <see cref="HandlerName"/>) index makes
/// redelivery a no-op, giving handlers at-least-once-with-dedup semantics.
/// </summary>
public sealed class InboxMessage : Entity
{
    /// <summary>The processed event's <see cref="OutboxMessage.EventId"/>.</summary>
    public Guid EventId { get; set; }

    /// <summary>Fully-qualified type name of the handler that processed the event.</summary>
    public string HandlerName { get; set; } = string.Empty;

    /// <summary>When the handler completed.</summary>
    public DateTime ProcessedAt { get; set; }
}
