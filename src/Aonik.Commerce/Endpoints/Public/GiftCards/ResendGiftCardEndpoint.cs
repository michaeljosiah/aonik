using Aonik.Commerce.Services.GiftCards;
using Aonik.Commerce.Endpoints.Public.Fulfilment;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Aonik.Commerce.Endpoints.Public.GiftCards;

public sealed class ResendGiftCardEndpoint(IGiftCardDeliveryService deliveries, ICurrentPartyResolver parties)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Post("/commerce/storefront/gift-cards/{deliveryId:guid}/resend");
        Policies("AdminUserPolicy");
        Options(builder => builder.RequireRateLimiting(GetDeliveryCoverageEndpoint.RateLimitPolicyName));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null) { await Send.NotFoundAsync(ct); return; }
        await deliveries.RequestResendAsync(Route<Guid>("deliveryId"), partyId.Value, ct);
        await Send.StatusCodeAsync(StatusCodes.Status202Accepted, ct);
    }
}
