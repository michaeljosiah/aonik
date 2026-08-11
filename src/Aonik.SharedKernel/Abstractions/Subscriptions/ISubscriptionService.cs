namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// The subscription lifecycle (Spec 087 §7, §12). Owns what a subscriber is on and when it renews;
/// it never moves money itself — a period raises an <c>Order</c>, an <c>Invoice</c> and a
/// <c>PaymentIntent</c> through the existing rails, exactly as every other order type does.
///
/// Every call is authorised through <see cref="ISubscriberAuthorizer"/>.
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    /// Start a subscription on the current version of <paramref name="planCode"/>. The version is
    /// pinned, so a later price rise never re-prices this subscriber.
    /// </summary>
    /// <param name="paymentMandateId">
    /// A stored mandate (Spec 088 §6) to charge on renewal. Optional only because a zero-price plan
    /// needs no funding; a priced plan without one cannot renew.
    /// </param>
    /// <exception cref="InvalidStateException">The subscriber already holds a subscription occupying the active slot.</exception>
    Task<SubscriptionDto> SubscribeAsync(
        SubscriberRef subscriber,
        string planCode,
        Guid? paymentMandateId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a plan change to take effect at the <b>next</b> period boundary. Held as a pending
    /// version and applied only when that period settles — an unpaid upgrade must confer nothing,
    /// and the current version has to remain readable until it is paid for.
    /// </summary>
    Task<SubscriptionDto> ChangePlanAsync(
        Guid subscriptionId,
        string planCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel. With <paramref name="atPeriodEnd"/> the subscription serves out the period it has
    /// paid for and is closed by the renewal job rather than renewed; without it, immediately.
    /// </summary>
    Task<SubscriptionDto> CancelAsync(
        Guid subscriptionId,
        bool atPeriodEnd = true,
        CancellationToken cancellationToken = default);

    /// <summary>Clear a pending cancellation, before the period boundary makes it final.</summary>
    Task<SubscriptionDto> ResumeAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>Attach or replace the mandate future renewals charge.</summary>
    Task<SubscriptionDto> SetPaymentMandateAsync(
        Guid subscriptionId,
        Guid paymentMandateId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionDto?> GetAsync(Guid subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>The subscriber's subscription in the active slot, or null if they have none.</summary>
    Task<SubscriptionDto?> GetForSubscriberAsync(SubscriberRef subscriber, CancellationToken cancellationToken = default);
}
