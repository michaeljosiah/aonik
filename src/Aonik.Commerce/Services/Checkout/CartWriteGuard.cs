using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;

namespace Aonik.Commerce.Services.Checkout;

internal static class CartWriteGuard
{
    public static bool IsEditable(Cart cart) => cart.Status == CartStatuses.Open
        && (cart.CheckoutState == CartCheckoutStates.Retryable
            || cart.OrderId is null && cart.CheckoutState is null);

    public static bool ProtectsStock(Cart cart) => cart.CheckoutState is CartCheckoutStates.Preparing or CartCheckoutStates.AwaitingPayment
        || cart.OrderId is not null && cart.CheckoutState != CartCheckoutStates.Retryable;

    // Call only after authorization: version and completion state belong to the cart's owner.
    public static void RequireCurrent(Cart cart, CartAccessContext access)
    {
        if (!IsEditable(cart))
            throw new CartWriteConflictException(cart, "commerce.cart_locked", "This cart is no longer editable. Reload its current state.");
        if (!ActiveBoxCarts.MatchesVersion(cart, access.ExpectedCartVersion))
            throw new CartWriteConflictException(cart, "commerce.cart_conflict", "The cart changed. Reload it before saving your changes.");
    }
}
