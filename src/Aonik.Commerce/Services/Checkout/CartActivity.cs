using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions;

namespace Aonik.Commerce.Services.Checkout;

internal static class CartActivity
{
    public static DateTime LastActivity(Cart cart) => cart.LastActivityAtUtc ?? cart.UpdatedAt ?? cart.CreatedAt;

    public static void UserEdit(CommerceDbContext context, Cart cart, IClock clock)
    {
        cart.LastActivityAtUtc = clock.UtcNow;
        Touch(context, cart, clock);
    }

    public static void ServerEdit(CommerceDbContext context, Cart cart, IClock clock)
    {
        cart.LastActivityAtUtc ??= LastActivity(cart);
        Touch(context, cart, clock);
    }

    private static void Touch(CommerceDbContext context, Cart cart, IClock clock)
    {
        cart.UpdatedAt = clock.UtcNow;
        context.Entry(cart).Property(c => c.UpdatedAt).IsModified = true;
    }
}
