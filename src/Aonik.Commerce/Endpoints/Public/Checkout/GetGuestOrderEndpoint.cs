using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public class GetGuestOrderEndpoint : EndpointWithoutRequest<StorefrontOrderDetailDto>
{
    private readonly IStorefrontOrderService _orders;

    public GetGuestOrderEndpoint(IStorefrontOrderService orders) => _orders = orders;

    public override void Configure()
    {
        Get("/commerce/storefront/guest-orders/{orderId:guid}");
        AllowAnonymous();
        Summary(s => s.Summary = "Read one guest order using its checkout-issued X-Order-Token.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";

        var tokens = HttpContext.Request.Headers["X-Order-Token"];
        if (tokens.Count != 1)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var detail = await _orders.GetGuestOrderAsync(Route<Guid>("orderId"), tokens[0], ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(detail, ct);
    }
}
