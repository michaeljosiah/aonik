using Aonik.Commerce.Contracts.Api.Checkout;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public class CheckoutEndpoint : Endpoint<CheckoutRequest, CheckoutResult>
{
    private readonly ICheckoutService _checkout;
    public CheckoutEndpoint(ICheckoutService checkout) => _checkout = checkout;

    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/checkout");
        AllowAnonymous();
        Summary(s => s.Summary = "Check out a cart: reserve stock, create the order, and initiate payment.");
    }

    public override async Task HandleAsync(CheckoutRequest req, CancellationToken ct)
    {
        var result = await _checkout.CheckoutAsync(
            new CheckoutCommand(
                Route<Guid>("cartId"),
                req.Provider,
                req.PaymentMethodType,
                req.ReturnUrl,
                req.CancelUrl,
                req.CustomerAccountId,
                req.DiscountCode),
            await CartRequestAccess.FromAsync(HttpContext, ct), ct);
        await Send.OkAsync(result, ct);
    }
}
