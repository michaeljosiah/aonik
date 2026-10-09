using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class ApplyCartDiscountEndpoint(ICartService carts) : Endpoint<CartDiscountRequest, CartDiscountQuoteDto>
{
    public override void Configure()
    {
        Put("/commerce/carts/{cartId:guid}/discount");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CartDiscountRequest req, CancellationToken ct)
        => await Send.OkAsync(await carts.ApplyDiscountAsync(Route<Guid>("cartId"), req.Code,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
}
