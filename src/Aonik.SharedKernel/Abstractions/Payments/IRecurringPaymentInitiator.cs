namespace Aonik.SharedKernel.Abstractions.Payments;

/// <summary>
/// Funds an order by charging a stored mandate, with nobody present (Spec 088 §6). Implemented by
/// <c>Aonik.Finance</c>.
///
/// The sibling <see cref="IPaymentInitiator"/> cannot serve this: it mints a <em>guest</em> intent
/// on the public payment path and requires a provider and payment-method type supplied per call.
/// A renewal job running unattended has neither value to offer, and "guest" is the wrong semantic
/// for charging a saved instrument under a standing authorisation. Here the <b>mandate</b> supplies
/// both.
/// </summary>
public interface IRecurringPaymentInitiator
{
    /// <summary>
    /// Charge <paramref name="mandateId"/> for <paramref name="orderId"/>.
    /// </summary>
    /// <param name="idempotencyKey">
    /// Required. Re-issuing the same key returns the existing intent rather than charging again —
    /// the difference between a retried job and a double charge.
    /// </param>
    /// <exception cref="MandateUnavailableException">
    /// The mandate is missing, revoked, expired, or belongs to another tenant. <b>Not retryable</b>:
    /// no amount of retrying restores a withdrawn authorisation, so a caller must tell this apart
    /// from a soft decline and route the customer to re-authorise instead of looping.
    /// </exception>
    Task<PaymentIntentRef> CreateIntentForMandateAsync(
        Guid mandateId,
        Guid orderId,
        decimal amount,
        string currency,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A stored authorisation cannot be charged (Spec 088 §6). Deliberately its own type rather than a
/// general failure: the caller's correct response is to stop and ask the customer to
/// re-authorise, which is the opposite of what it should do for a soft decline.
/// </summary>
public sealed class MandateUnavailableException : Exception
{
    public MandateUnavailableException(Guid mandateId, string reason)
        : base($"Payment mandate '{mandateId}' cannot be charged: {reason}.")
    {
        MandateId = mandateId;
        Reason = reason;
    }

    public Guid MandateId { get; }

    /// <summary>Why it is unusable — missing, revoked, expired. Safe to surface to an operator.</summary>
    public string Reason { get; }

    /// <summary>
    /// Always false. Present so a caller branching on retryability reads the intent rather than
    /// inferring it from the type.
    /// </summary>
    public bool IsRetryable => false;
}
