using Microsoft.EntityFrameworkCore;

using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Entities.Cart;
using Aonik.Commerce.Persistence;
using Aonik.SharedKernel.Abstractions.Identity;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Ordering;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Commerce.Services.Checkout;

internal sealed class PaidCheckoutAccountAccessService(
    CommerceDbContext context,
    ITenantProvider tenantProvider,
    IOrderService orders,
    IPaidAccountAccessService accountAccess) : IPaidCheckoutAccountAccessService
{
    public async Task IssueAsync(Guid orderId, Guid paymentIntentId, CancellationToken cancellationToken = default)
    {
        var source = await LoadPaidSourceAsync(orderId, paymentIntentId, cancellationToken);
        if (source is null || source.Value.Cart.BuyerPartyId is not null) return;
        var (cart, preparation) = source.Value;
        var email = (preparation.Purchaser ?? preparation.Delivery?.Purchaser)?.Email;
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("The opted-in paid checkout is missing its purchaser contact snapshot.");

        // Platform deduplicates the paid source before enqueueing delivery. A retry cannot create
        // another access action or use an address subsequently edited in the cart draft.
        await accountAccess.IssueAsync(new PaidAccountAccessRequest(cart.TenantId, cart.Id, orderId,
            paymentIntentId, preparation.GuestPartyId!.Value, email), cancellationToken);
    }

    public async Task LinkAsync(AccountAccessVerifiedEvent verified, CancellationToken cancellationToken = default)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        if (verified.TenantId != tenantId || verified.ActionId == Guid.Empty || verified.AccountPartyId == Guid.Empty)
            throw new InvalidOperationException("The verified account access does not match this checkout.");

        for (var attempt = 0; ; attempt++)
        {
            CartTracking.Detach(context, tenantId, verified.CartId);
            var source = await LoadPaidSourceAsync(verified.OrderId, verified.PaymentIntentId, cancellationToken)
                ?? throw new InvalidOperationException("The verified account access does not match a paid checkout.");
            var (cart, preparation) = source;
            if (cart.Id != verified.CartId || preparation.GuestPartyId != verified.GuestPartyId)
                throw new InvalidOperationException("The verified account access does not match this checkout.");
            if (cart.BuyerPartyId == verified.AccountPartyId) return;
            if (cart.BuyerPartyId is not null)
                throw new InvalidOperationException("The paid checkout already belongs to another account.");

            try
            {
                context.Attach(cart);
                cart.BuyerPartyId = verified.AccountPartyId;
                // The cart's native rowversion arbitrates competing verified claims. Financial
                // payer references, the frozen preparation and guest capabilities remain intact.
                await context.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 2)
            {
                CartTracking.Detach(context, tenantId, cart.Id);
            }
            catch
            {
                CartTracking.Detach(context, tenantId, cart.Id);
                throw;
            }
        }
    }

    private async Task<(Cart Cart, CheckoutPreparation Preparation)?> LoadPaidSourceAsync(
        Guid orderId, Guid paymentIntentId, CancellationToken cancellationToken)
    {
        var tenantId = tenantProvider.GetCurrentTenantId();
        var cart = await context.Carts.AsNoTracking().SingleOrDefaultAsync(
            row => row.TenantId == tenantId && row.OrderId == orderId, cancellationToken);
        if (cart is null || cart.Status != CartStatuses.CheckedOut || cart.CheckoutPreparationJson is null) return null;
        var preparation = CheckoutPreparation.Read(cart);
        if (!preparation.CreateAccount || preparation.GuestPartyId is not { } guestPartyId || guestPartyId == Guid.Empty
            || preparation.AttemptId != paymentIntentId) return null;

        var summary = await context.OrderChargeSummaries.AsNoTracking().SingleOrDefaultAsync(
            row => row.TenantId == tenantId && row.OrderId == orderId, cancellationToken);
        if (summary is null || summary.PaymentIntentId != paymentIntentId
            || summary.PaymentStatus != CheckoutPaymentStatuses.Captured || summary.Total != preparation.Total
            || !string.Equals(summary.Currency, preparation.Currency, StringComparison.OrdinalIgnoreCase)) return null;

        var order = await orders.GetAsync(orderId, cancellationToken);
        if (order is null || order.TenantId != tenantId || order.Status != OrderStatusCodes.Complete
            || order.PayerPartyId != guestPartyId
            || !string.Equals(order.CurrencyIn, preparation.Currency, StringComparison.OrdinalIgnoreCase)) return null;
        return (cart, preparation);
    }
}
