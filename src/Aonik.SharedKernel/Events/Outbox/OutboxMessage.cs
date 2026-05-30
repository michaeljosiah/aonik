using Aonik.SharedKernel.Primitives;

namespace Aonik.SharedKernel.Events.Outbox;

/// <summary>
/// Durable record of an integration event awaiting asynchronous dispatch.
/// Written in the same transaction as the originating domain change (the
/// "transactional outbox" pattern) so an event is never lost to a crash between
/// committing the change and publishing its event. Drained by the outbox
/// processor running in the Worker host.
/// </summary>
public sealed class OutboxMessage : Entity
{
    /// <summary>Stable idempotency / correlation key, captured once at enqueue time.</summary>
    public Guid EventId { get; set; }

    /// <summary><see cref="System.Type.FullName"/> of the event, used to rehydrate the payload.</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>JSON-serialized event body.</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>Tenant the event was raised under; restored before dispatch. Null for global events.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>When the domain event occurred.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>When the row was enqueued.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>When every handler completed successfully. Null while pending.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>Number of dispatch attempts so far.</summary>
    public int Attempts { get; set; }

    /// <summary>Earliest time the next attempt may run (exponential backoff gate). Null = eligible now.</summary>
    public DateTime? NextAttemptAt { get; set; }

    /// <summary>Last dispatch error (truncated). Null when healthy.</summary>
    public string? Error { get; set; }

    /// <summary>When the message was abandoned after exhausting retries. Null unless dead-lettered.</summary>
    public DateTime? DeadLetteredAt { get; set; }

    /// <summary>W3C traceparent captured at enqueue, for cross-process trace continuity.</summary>
    public string? TraceParent { get; set; }
}
