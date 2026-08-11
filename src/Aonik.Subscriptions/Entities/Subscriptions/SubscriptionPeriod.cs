using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Primitives;

namespace Aonik.Subscriptions.Entities.Subscriptions;

/// <summary>
/// One billed period (Spec 087 §7). Not bookkeeping for its own sake — it is the
/// <b>idempotency anchor</b> for renewal: the job keys on <c>(SubscriptionId, Sequence)</c>, so a
/// job that runs twice, or dies between minting the order and taking payment, cannot double-bill.
///
/// The invoice and intent ids are persisted the moment each writer returns, because the anchor
/// alone protects the order and nothing else.
/// </summary>
public class SubscriptionPeriod : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }

    public Guid SubscriptionId { get; set; }

    /// <summary>Monotonic per subscription, starting at 1.</summary>
    public int Sequence { get; set; }

    public DateTime StartsAt { get; set; }

    public DateTime EndsAt { get; set; }

    /// <summary>The order this period raised. Null only for a zero-total period before it is created.</summary>
    public Guid? OrderId { get; set; }

    public Guid? InvoiceId { get; set; }

    /// <summary>The current payment attempt. Prior attempts remain as history — a failed intent is terminal and must be replaced, not reused.</summary>
    public Guid? PaymentIntentId { get; set; }

    /// <summary>One of <c>SubscriptionPeriodStatuses</c>.</summary>
    public string Status { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTime? NextAttemptAt { get; set; }
}
