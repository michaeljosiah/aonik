using Aonik.Commerce.Contracts.Models.Checkout;

namespace Aonik.Commerce.Services.Checkout;

/// <summary>
/// Checkout orchestration (Spec 042 §11): reserve inventory, create a <c>ProductPurchase</c> order
/// via the SharedKernel Ordering contract, record build-your-own-box contents, optionally raise an
/// invoice, initiate funding via the SharedKernel payment contract, and link funding to the order.
/// Capture stays a Finance high-tier action — Commerce never moves money.
/// </summary>
public interface ICheckoutService
{
    /// <summary>R10 — checkout presents the cart access context like every other cart
    /// operation; an unauthorized caller gets the same 404 an unknown cart id gets.</summary>
    Task<CheckoutResult> CheckoutAsync(CheckoutCommand command, CartAccessContext access, CancellationToken cancellationToken = default);

    Task<CartPaymentStateDto> GetPaymentStateAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);

    /// <summary>Read-only recovery of a captured guest order capability for the authorized cart owner.</summary>
    Task<CartPaymentStateDto> GetPaymentConfirmationStateAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);

    Task<CartPaymentStateDto> RecoverAsync(Guid cartId, Guid expectedPaymentIntentId, CartAccessContext access,
        CancellationToken cancellationToken = default);

    /// <summary>Converges only the recorded intent and exact money. True also permits an
    /// already-completed matching event to retry its confirmation email.</summary>
    Task<bool> ConfirmPaymentAsync(Guid orderId, Guid completedPaymentIntentId, decimal amount, string currency,
        CancellationToken cancellationToken = default);

    /// <summary>System discovery only; each result is reconciled in a fresh tenant scope.</summary>
    Task<IReadOnlyList<(Guid ReservationId, Guid TenantId, Guid CartId)>> FindDueDeliveryReservationsAsync(
        Guid? afterReservationId = null, CancellationToken cancellationToken = default);

    Task ReconcileDeliveryReservationAsync(Guid cartId, CancellationToken cancellationToken = default);
}
