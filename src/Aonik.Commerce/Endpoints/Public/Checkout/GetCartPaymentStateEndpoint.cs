using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Endpoints.Public.Fulfilment;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class GetCartPaymentStateEndpoint(ICheckoutService checkout) : EndpointWithoutRequest<CartPaymentStateDto>
{
    public override void Configure()
    {
        Get("/commerce/carts/{cartId:guid}/payment");
        AllowAnonymous();
        Options(builder => builder.RequireRateLimiting(GetDeliveryCoverageEndpoint.RateLimitPolicyName));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        await Send.OkAsync(await checkout.GetPaymentStateAsync(Route<Guid>("cartId"),
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
    }
}
