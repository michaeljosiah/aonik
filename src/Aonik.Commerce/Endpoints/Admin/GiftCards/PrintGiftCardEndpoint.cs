using Aonik.Authorization;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Services.GiftCards;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.GiftCards;

public sealed class PrintGiftCardEndpoint(IGiftCardDeliveryService deliveries) : EndpointWithoutRequest<GiftCardPrintDto>
{
    public override void Configure()
    {
        Post("/commerce/admin/gift-card-deliveries/{deliveryId:guid}/print");
        Policies("AdminWritePolicy");
        this.RequiresPermission("Customers.Write");
        Summary(summary => summary.Summary = "Read the private gift card print document and record the staff print action.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        await Send.OkAsync(await deliveries.PrintAsync(Route<Guid>("deliveryId"), ct), ct);
    }
}
