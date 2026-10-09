using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public record CartDiscountRequest(string Code);

public sealed class ValidateCartDiscountEndpoint(ICartService carts) : Endpoint<CartDiscountRequest, CartDiscountQuoteDto>
{
    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/discount/validate");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CartDiscountRequest req, CancellationToken ct)
        => await Send.OkAsync(await carts.PreviewDiscountAsync(Route<Guid>("cartId"), req.Code,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
}
