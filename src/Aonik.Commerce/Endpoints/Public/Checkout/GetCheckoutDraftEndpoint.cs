using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class GetCheckoutDraftEndpoint(ICartService carts) : EndpointWithoutRequest<CartCheckoutDraftResponse>
{
    public override void Configure()
    {
        Get("/commerce/carts/{cartId:guid}/checkout-draft");
        AllowAnonymous();
        Summary(s => s.Summary = "Read the saved checkout form even when the box cannot currently be quoted.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var cart = await carts.GetCartAsync(Route<Guid>("cartId"), await CartRequestAccess.FromAsync(HttpContext, ct), ct);
        if (cart is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(new CartCheckoutDraftResponse(cart.Id, cart.CartVersion, cart.Status, cart.OrderId, cart.CheckoutDraft), ct);
    }
}
