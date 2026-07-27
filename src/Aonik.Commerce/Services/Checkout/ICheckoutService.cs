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

    /// <summary>
    /// On payment completion for a checkout order (driven by <c>PaymentCompletedEvent</c>), commits
    /// the held inventory, closes the cart, and transitions the order to Complete. Idempotent.
    /// </summary>
    /// <param name="completedPaymentIntentId">
    /// The intent the event actually completed. Finance permits SEVERAL intents per order, so the
    /// charge summary's payment status converges only when this matches the intent checkout
    /// recorded — a different intent (potentially a different amount or currency) must never mark
    /// this checkout's charge captured. Null means "unknown/legacy caller": the funding side
    /// effects still run, but the summary's status is left alone rather than guessed.
    /// </param>
    Task ConfirmPaymentAsync(Guid orderId, Guid? completedPaymentIntentId = null, CancellationToken cancellationToken = default);
}
