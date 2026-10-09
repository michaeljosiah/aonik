using Aonik.Authorization;
using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Services.GiftCards;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.GiftCards;

public sealed class ListPhysicalGiftCardsEndpoint(IGiftCardDeliveryService deliveries)
    : EndpointWithoutRequest<PagedResult<PhysicalGiftCardDto>>
{
    public override void Configure()
    {
        Get("/commerce/admin/gift-card-deliveries");
        Policies("AdminReadPolicy");
        this.RequiresPermission("Customers.Read");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        var page = Query<int?>("page", isRequired: HttpContext.Request.Query.ContainsKey("page")) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: HttpContext.Request.Query.ContainsKey("pageSize")) ?? 20;
        var dueThrough = Query<DateOnly?>("dueThrough", isRequired: HttpContext.Request.Query.ContainsKey("dueThrough"));
        await Send.OkAsync(await deliveries.ListPhysicalAsync(dueThrough, page, pageSize, ct), ct);
    }
}
