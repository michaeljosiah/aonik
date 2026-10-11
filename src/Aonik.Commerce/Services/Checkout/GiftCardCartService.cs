using System.Text.Json;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.Commerce.Services.Catalog;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.GiftCards;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Microsoft.EntityFrameworkCore;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class GiftCardCartService(CommerceDbContext db, ITenantProvider tenantProvider, IClock clock,
    GiftCardPurchasePricing pricing, IGiftCardService gifts, CartDiscountQuotes quotes)
{
    public async Task<GiftCardCartResponse> SetPurchaseAsync(Guid cartId, GiftCardPurchaseSelection? selection,
        CartAccessContext access, CancellationToken ct = default)
    {
        var cart = await LoadAsync(cartId, access, ct);
        GiftCardPurchaseDto? purchase = selection == null ? null : await pricing.SelectAsync(cart, selection, ct);
        CartItem? replacement = null;
        if (purchase != null)
        {
            var policy = await pricing.PolicyAsync(ct);
            if (policy.Version != purchase.Selection.AcceptedVersion)
                throw new StorefrontValidationException("Gift-card options changed. Review the current options.");
            var variant = await pricing.VariantAsync(policy.Store.ProductVariantId, ct);
            replacement = new CartItem { TenantId = cart.TenantId, CartId = cart.Id,
                ProductVariantId = variant.Id, Quantity = purchase.Selection.Quantity, UnitPriceSnapshot = purchase.Selection.FaceValue,
                LineKind = CartLineKinds.GiftCardValue, Sku = variant.Sku, NameSnapshot = variant.Name };
        }
        var existing = cart.Items.Where(x => x.LineKind == CartLineKinds.GiftCardValue).ToList();
        db.CartItems.RemoveRange(existing);
        foreach (var item in existing) cart.Items.Remove(item);
        if (replacement != null)
        {
            db.CartItems.Add(replacement);
        }
        cart.GiftCardPurchaseJson = purchase == null ? null : JsonSerializer.Serialize(purchase, GiftCardPurchasePricing.Json);
        await SaveAsync(cart, ct);
        return await ResponseAsync(cartId, access, ct);
    }

    public async Task<GiftCardCartResponse> SetTenderAsync(Guid cartId, string? code, decimal requestedAmount,
        CartAccessContext access, CancellationToken ct = default)
    {
        var cart = await LoadAsync(cartId, access, ct);
        if (code == null) cart.GiftCardTenderJson = null;
        else
        {
            if (cart.Currency != "GBP") throw new StorefrontValidationException("Gift-card payment requires GBP checkout.");
            if (code.Length is < 1 or > 128 || requestedAmount <= 0m || decimal.Round(requestedAmount, 2) != requestedAmount)
                throw new StorefrontValidationException("Enter a gift-card code and a positive amount in whole pennies.");
            if (cart.GiftCardPurchaseJson != null || cart.Items.Any(x => x.LineKind == CartLineKinds.GiftCardValue))
                throw new StorefrontValidationException("A gift card cannot fund a gift-card purchase.");
            var grant = await gifts.AuthorizeForCartAsync(code, cart.Id, ct);
            if (grant.PrivateCartGrant == null || grant.Card == null)
                throw new StorefrontValidationException("The gift card is not available for this checkout.");
            cart.GiftCardTenderJson = JsonSerializer.Serialize(new StoredGiftCardTender(grant.PrivateCartGrant,
                requestedAmount, grant.Card.MaskedCode), GiftCardPurchasePricing.Json);
        }
        await SaveAsync(cart, ct);
        return await ResponseAsync(cartId, access, ct);
    }

    private async Task<Cart> LoadAsync(Guid id, CartAccessContext access, CancellationToken ct)
    {
        var tenant = tenantProvider.GetCurrentTenantId();
        CartTracking.Detach(db, tenant, id);
        var cart = await db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant, ct);
        if (cart == null || !CartAccess.IsAuthorized(cart, access)) throw new NotFoundException("Cart was not found.");
        CartWriteGuard.RequireCurrent(cart, access);
        return cart;
    }

    private async Task SaveAsync(Cart cart, CancellationToken ct)
    {
        CartActivity.UserEdit(db, cart, clock);
        try { await db.SaveChangesAsync(ct); }
        catch { CartTracking.Detach(db, cart.TenantId, cart.Id); throw; }
    }

    private async Task<GiftCardCartResponse> ResponseAsync(Guid id, CartAccessContext access, CancellationToken ct)
    {
        var cart = await db.Carts.AsNoTracking().Include(x => x.Items)
            .SingleAsync(x => x.Id == id && x.TenantId == tenantProvider.GetCurrentTenantId(), ct);
        if (!CartAccess.IsAuthorized(cart, access)) throw new NotFoundException("Cart was not found.");
        return new(cart.Id, Convert.ToBase64String(cart.RowVersion), GiftCardPurchasePricing.Read(cart),
            await quotes.SnapshotAsync(cart, CartDraftData.Read(cart)?.DiscountCode, ct));
    }
}
