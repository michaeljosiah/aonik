using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class RemoveCartDiscountEndpoint(ICartService carts) : EndpointWithoutRequest<CartDiscountQuoteDto>
{
    public override void Configure()
    {
        Delete("/commerce/carts/{cartId:guid}/discount");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await carts.RemoveDiscountAsync(Route<Guid>("cartId"),
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
}
