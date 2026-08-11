namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// Buying units of a meter outright (Spec 087 §12.4) — a top-up, not a subscription. Purchased
/// grants never expire and are drawn on last, after perishable plan allowance.
///
/// Pricing is resolved <b>server-side</b> from the meter's current offer. There is deliberately no
/// caller-supplied price on this contract: accepting one would let units be bought at any amount.
/// </summary>
public interface IEntitlementPurchaseService
{
    /// <summary>
    /// Raise an <c>EntitlementPurchase</c> order for <paramref name="quantity"/> units, priced from
    /// the current offer. The offer version is recorded on the order line, so a later price change
    /// cannot restate a completed purchase. Funding and settlement follow the normal order rails;
    /// the grant is materialised only once payment settles.
    /// </summary>
    /// <exception cref="NotFoundException">No active offer exists for the meter in this tenant.</exception>
    /// <exception cref="InvalidStateException"><paramref name="quantity"/> falls outside the offer's permitted range.</exception>
    Task<EntitlementPurchaseRef> CreateAsync(
        SubscriberRef subscriber,
        string meterCode,
        decimal quantity,
        CancellationToken cancellationToken = default);
}
