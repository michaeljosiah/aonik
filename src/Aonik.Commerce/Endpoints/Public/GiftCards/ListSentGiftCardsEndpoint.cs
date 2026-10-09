using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.Commerce.Services.GiftCards;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.GiftCards;

public sealed class ListSentGiftCardsEndpoint(IGiftCardDeliveryService deliveries, ICurrentPartyResolver parties)
    : EndpointWithoutRequest<Aonik.Commerce.Contracts.Models.Catalog.PagedResult<SentGiftCardDto>>
{
    public override void Configure()
    {
        Get("/commerce/storefront/gift-cards");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        HttpContext.Response.Headers["Referrer-Policy"] = "no-referrer";
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null) { await Send.NotFoundAsync(ct); return; }
        var page = Query<int?>("page", isRequired: HttpContext.Request.Query.ContainsKey("page")) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: HttpContext.Request.Query.ContainsKey("pageSize")) ?? 20;
        await Send.OkAsync(await deliveries.ListSentAsync(partyId.Value, page, pageSize, ct), ct);
    }
}
