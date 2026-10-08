using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;

using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

internal static class CartTracking
{
    // A new operation must not reuse a stale owner/version or flush a rejected prior edit.
    public static void Detach(CommerceDbContext context, Guid tenantId, Guid cartId)
    {
        var items = context.ChangeTracker.Entries<CartItem>()
            .Where(e => e.Entity.TenantId == tenantId && e.Entity.CartId == cartId).ToList();
        var itemIds = items.Select(e => e.Entity.Id).ToHashSet();
        foreach (var entry in context.ChangeTracker.Entries<CartItemSelection>()
                     .Where(e => e.Entity.TenantId == tenantId && itemIds.Contains(e.Entity.CartItemId)).ToList())
            entry.State = EntityState.Detached;
        foreach (var item in items)
        {
            item.Entity.Selections.Clear();
            item.State = EntityState.Detached;
        }
        foreach (var cart in context.ChangeTracker.Entries<Cart>()
                     .Where(e => e.Entity.TenantId == tenantId && e.Entity.Id == cartId).ToList())
        {
            cart.Entity.Items.Clear();
            cart.State = EntityState.Detached;
        }
    }
}
