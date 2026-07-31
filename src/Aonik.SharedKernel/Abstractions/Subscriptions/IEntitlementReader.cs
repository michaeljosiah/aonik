namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// Read access to what a subscriber currently has (Spec 087 §14.1) — the query a product's UI
/// renders, and the non-throwing pre-check a caller makes before attempting metered work that
/// <see cref="IUsageMeter"/> would otherwise refuse.
///
/// Always reports the last <b>settled</b> entitlement set: a pending plan change or an unpaid
/// renewal never shows here, because a subscriber must not see capability they have not paid for.
/// </summary>
public interface IEntitlementReader
{
    /// <summary>Plan, period and every meter's standing. Null when the subscriber holds no subscription.</summary>
    Task<EntitlementSnapshot?> GetAsync(
        SubscriberRef subscriber,
        CancellationToken cancellationToken = default);

    /// <summary>One meter's standing, without loading the rest. Null when the subscriber has no subscription, or the plan grants no such meter.</summary>
    Task<MeterEntitlement?> GetMeterAsync(
        SubscriberRef subscriber,
        string meterCode,
        CancellationToken cancellationToken = default);
}
