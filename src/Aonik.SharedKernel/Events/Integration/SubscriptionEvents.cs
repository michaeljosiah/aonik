namespace Aonik.SharedKernel.Events.Integration;

// ── Subscription-originated integration events (Spec 087) ───────────────────

/// <summary>
/// Raised when usage has been committed against a subscriber's entitlement.
/// </summary>
/// <param name="UsageRecordId">
/// The record the commit wrote. Everything the ledger needs is reachable from it, and it is also the
/// idempotency key the journal is posted under.
/// </param>
/// <remarks>
/// This event exists to make entitlement state and the ledger recover together. The commit writes
/// the grant drawdown and the usage record through the Subscriptions context, while the matching
/// 2210/4110 and 5100 postings go through Finance's <c>IJournalWriter</c> — two module contexts, two
/// connections, and therefore two transactions. Posting inline left a crash able to consume
/// allowance without its journal entry.
///
/// Staged in the outbox by the <b>same save</b> as the drawdown, so the event cannot describe a
/// commit that did not happen; and the journal post is keyed on the usage record, so redelivery
/// cannot recognise the same revenue twice. Durable and idempotent in both directions, which is what
/// makes retrying safe.
/// </remarks>
public record UsageCommittedEvent(
    Guid TenantId,
    Guid UsageRecordId) : IIntegrationEvent;
