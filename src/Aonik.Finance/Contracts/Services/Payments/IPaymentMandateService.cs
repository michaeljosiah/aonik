using Aonik.Finance.Contracts.Models.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

/// <summary>
/// The lifecycle of a customer's standing authorisation to be charged (Spec 088 §6).
///
/// Charging is deliberately not here — that is <c>IRecurringPaymentInitiator</c>, reachable from
/// outside Finance. This interface is the authoring side, and it stays Finance-internal because
/// recording consent is not something another module should be able to do on a customer's behalf.
/// </summary>
public interface IPaymentMandateService
{
    /// <summary>
    /// Record a customer's authorisation to charge a vaulted method in future.
    /// </summary>
    /// <remarks>
    /// <b>Requires an interactive caller.</b> A mandate records a human's consent, so it must
    /// originate in a moment where that human was present — a successful interactive payment, or an
    /// explicit "save for future payments". Attempting this from a background job throws: a job has
    /// no current user, and a mandate minted without one would be an authorisation nobody gave.
    /// </remarks>
    /// <exception cref="InvalidStateException">No current user, or the payment method is unusable.</exception>
    Task<PaymentMandateResponse> CreateAsync(CreatePaymentMandateRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraw a mandate immediately. Idempotent — revoking an already-revoked or expired mandate
    /// is a no-op rather than an error, because the customer's intent is satisfied either way.
    /// </summary>
    Task<PaymentMandateResponse> RevokeAsync(Guid mandateId, string reason, CancellationToken cancellationToken = default);

    /// <summary>The party's chargeable mandate, or null. Expired mandates are never returned as active.</summary>
    Task<PaymentMandateResponse?> GetActiveForPartyAsync(Guid partyId, CancellationToken cancellationToken = default);

    Task<PaymentMandateResponse?> GetAsync(Guid mandateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revoke every mandate charging a payment method — for a card the provider tells us has been
    /// replaced or cancelled. The authorisation follows the party, but it cannot outlive the
    /// instrument it names.
    /// </summary>
    Task<int> RevokeForPaymentMethodAsync(Guid paymentMethodId, string reason, CancellationToken cancellationToken = default);
}
