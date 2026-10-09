using System.Text.Json.Serialization;

using Aonik.Authorization;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Services.GiftCards;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.GiftCards;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompletePhysicalGiftCardRequest([property: JsonRequired] string ExpectedVersion);

public sealed class CompletePhysicalGiftCardEndpoint(IGiftCardDeliveryService deliveries)
    : Endpoint<CompletePhysicalGiftCardRequest, PhysicalGiftCardDto>
{
    public override void Configure()
    {
        Post("/commerce/admin/gift-card-deliveries/{deliveryId:guid}/complete");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Customers.Write");
    }

    public override async Task HandleAsync(CompletePhysicalGiftCardRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        await Send.OkAsync(await deliveries.CompletePhysicalAsync(Route<Guid>("deliveryId"), req.ExpectedVersion, ct), ct);
    }
}
