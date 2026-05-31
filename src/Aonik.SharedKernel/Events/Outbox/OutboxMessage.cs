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

    /// <summary>
    /// Identifier of the drainer instance currently holding the processing lease
    /// (machine name + a per-sweep token). Null when the row is unclaimed. Stops a
    /// second drainer from dispatching the same row concurrently.
    /// </summary>
    public string? ClaimedBy { get; set; }

    /// <summary>When the current processing lease was taken. Null when unclaimed.</summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>
    /// When the processing lease lapses and another drainer may reclaim the row.
    /// Lets a crashed drainer's in-flight rows recover automatically once the lease
    /// expires. Null when unclaimed.
    /// </summary>
    public DateTime? ClaimExpiresAt { get; set; }

    /// <summary>W3C traceparent captured at enqueue, for cross-process trace continuity.</summary>
    public string? TraceParent { get; set; }
}
