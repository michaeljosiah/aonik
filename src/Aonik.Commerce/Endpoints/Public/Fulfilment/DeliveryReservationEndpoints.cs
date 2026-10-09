using Aonik.Commerce.Contracts.Models.Fulfilment;
using Aonik.Commerce.Endpoints.Public.Checkout;
using Aonik.Commerce.Services.Fulfilment;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Fulfilment;

public sealed class GetCartDeliveryReservationEndpoint(IDeliveryReservationService reservations)
    : EndpointWithoutRequest<CartDeliveryReservationDto>
{
    public override void Configure()
    {
        Get("/commerce/carts/{cartId:guid}/delivery-reservation");
        AllowAnonymous();
        Summary(s => s.Summary = "Read this cart's delivery reservation and server time.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await reservations.GetAsync(Route<Guid>("cartId"), await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
    }
}

public sealed class ReserveCartDeliveryDateEndpoint(IDeliveryReservationService reservations)
    : Endpoint<ReserveDeliveryDateRequest, CartDeliveryReservationDto>
{
    public override void Configure()
    {
        Put("/commerce/carts/{cartId:guid}/delivery-reservation");
        AllowAnonymous();
        Summary(s => s.Summary = "Reserve or replace a delivery date using the current cart version.");
    }

    public override async Task HandleAsync(ReserveDeliveryDateRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await reservations.ReserveAsync(Route<Guid>("cartId"), req.DeliveryDate,
            await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
    }
}

public sealed class ReleaseCartDeliveryReservationEndpoint(IDeliveryReservationService reservations)
    : EndpointWithoutRequest<CartDeliveryReservationDto>
{
    public override void Configure()
    {
        Delete("/commerce/carts/{cartId:guid}/delivery-reservation");
        AllowAnonymous();
        Summary(s => s.Summary = "Release an unpaid date selection using the current cart version.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        await Send.OkAsync(await reservations.ReleaseAsync(Route<Guid>("cartId"), await CartRequestAccess.FromAsync(HttpContext, ct), ct), ct);
    }
}
