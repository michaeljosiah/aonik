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
    Task<CheckoutResult> CheckoutAsync(CheckoutCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// On payment completion for a checkout order (driven by <c>PaymentCompletedEvent</c>), commits
    /// the held inventory, closes the cart, and transitions the order to Complete. Idempotent.
    /// </summary>
    Task ConfirmPaymentAsync(Guid orderId, CancellationToken cancellationToken = default);
}
