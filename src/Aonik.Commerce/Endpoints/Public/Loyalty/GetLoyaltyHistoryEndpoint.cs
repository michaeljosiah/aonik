using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Loyalty;

public sealed class GetLoyaltyHistoryEndpoint(ILoyaltyService loyalty, ICurrentPartyResolver parties)
    : EndpointWithoutRequest<LoyaltyActivityPage>
{
    public override void Configure()
    {
        Get("/commerce/storefront/loyalty/history");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null) { await Send.NotFoundAsync(ct); return; }
        var page = Query<int?>("page", isRequired: HttpContext.Request.Query.ContainsKey("page")) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: HttpContext.Request.Query.ContainsKey("pageSize")) ?? 20;
        await Send.OkAsync(await loyalty.GetActivityAsync(partyId.Value, page, pageSize, ct), ct);
    }
}
