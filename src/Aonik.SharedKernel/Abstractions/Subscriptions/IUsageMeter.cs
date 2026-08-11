namespace Aonik.SharedKernel.Abstractions.Subscriptions;

/// <summary>
/// Enforcement of a subscriber's entitlements (Spec 087 §9). Metered work is authorised
/// <b>before</b> it runs and settled at its true cost afterwards — the same authorise/capture shape
/// the payments module uses, for the same reason: the work may cost less than expected, or not
/// happen at all.
///
/// Each meter kind has its own path. Only <see cref="MeterKinds.Counter"/> uses reserve/commit;
/// <see cref="MeterKinds.Ceiling"/> claims and releases slots, and <see cref="MeterKinds.Flag"/> is
/// a read. Every call is authorised through <see cref="ISubscriberAuthorizer"/> first — tenant
/// scope alone does not establish that a caller may act for a given subscriber.
/// </summary>
public interface IUsageMeter
{
    /// <summary>
    /// Hold <paramref name="quantity"/> of a counter meter against the subscriber's grants, in
    /// draw-down order (soonest-expiring first, purchased last). The hold is recorded per grant, so
    /// it can be returned to exactly the grants it came from.
    /// </summary>
    /// <param name="idempotencyKey">
    /// Caller-generated and unique per tenant. Replaying the same key returns the existing
    /// reservation rather than taking a second hold.
    /// </param>
    /// <param name="holdFor">How long before the sweep returns the hold. Implementation default when null.</param>
    /// <exception cref="EntitlementExceededException">Remaining allowance across all open grants is insufficient.</exception>
    Task<UsageReservationRef> ReserveAsync(
        SubscriberRef subscriber,
        string meterCode,
        decimal quantity,
        string idempotencyKey,
        TimeSpan? holdFor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Convert a hold into consumption at its actual quantity. When
    /// <paramref name="actualQuantity"/> is below the reserved amount the surplus is released from
    /// the <b>tail</b> of the draw-down order, so the consumed prefix keeps plan-before-purchase —
    /// trimming pro-rata would burn permanent units while returning perishable ones.
    /// A quantity above the reservation re-checks availability rather than assuming the hold covers it.
    /// </summary>
    /// <exception cref="InvalidStateException">The reservation is not <see cref="UsageReservationStatuses.Held"/>, or has expired.</exception>
    /// <exception cref="EntitlementExceededException"><paramref name="actualQuantity"/> exceeds the hold and the shortfall is unavailable.</exception>
    Task<UsageCommitResult> CommitAsync(
        Guid reservationId,
        decimal actualQuantity,
        UsageSource source,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Return a hold without charging — the work failed, or was abandoned. Idempotent: releasing an
    /// already-released or already-expired reservation is a no-op, not an error.
    /// </summary>
    Task ReleaseAsync(Guid reservationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Claim one slot of a ceiling meter for <paramref name="holderRef"/> — the stable identity of
    /// the object occupying it (e.g. a child profile id). Compare-and-increment, so concurrent
    /// callers at the limit cannot both succeed.
    ///
    /// Idempotent per holder: re-claiming for a holder that already holds a slot does not consume a
    /// second one. Call inside the same transaction as the object's creation, so a failed create
    /// returns the slot.
    /// </summary>
    /// <exception cref="EntitlementExceededException">The ceiling is already fully claimed.</exception>
    Task ClaimSlotAsync(
        SubscriberRef subscriber,
        string meterCode,
        string holderRef,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Release the slot held by <paramref name="holderRef"/>. This is what makes deleting a
    /// ceiling-metered object return its slot — the behaviour that distinguishes a ceiling from a
    /// counter, where deleting something must <b>not</b> return allowance. Idempotent.
    /// </summary>
    Task ReleaseSlotAsync(
        SubscriberRef subscriber,
        string meterCode,
        string holderRef,
        CancellationToken cancellationToken = default);

    /// <summary>Whether a flag meter is on for this subscriber. Reads the last <b>settled</b> entitlement set, so an unpaid upgrade confers nothing.</summary>
    Task<bool> HasFlagAsync(
        SubscriberRef subscriber,
        string meterCode,
        CancellationToken cancellationToken = default);
}
