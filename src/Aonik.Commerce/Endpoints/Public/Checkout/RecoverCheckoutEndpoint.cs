using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Endpoints.Public.Fulfilment;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public record RecoverCheckoutRequest(Guid PaymentIntentId);

public sealed class RecoverCheckoutEndpoint(ICheckoutService checkout) : Endpoint<RecoverCheckoutRequest, CartPaymentStateDto>
{
    public override void Configure()
    {
        Post("/commerce/carts/{cartId:guid}/payment/recover");
        AllowAnonymous();
        Options(builder => builder.RequireRateLimiting(GetDeliveryCoverageEndpoint.RateLimitPolicyName));
    }

    public override async Task HandleAsync(RecoverCheckoutRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        await Send.OkAsync(await checkout.RecoverAsync(Route<Guid>("cartId"), req.PaymentIntentId,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
    }
}
