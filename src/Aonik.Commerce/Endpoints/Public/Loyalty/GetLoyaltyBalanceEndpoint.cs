using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Loyalty;

public sealed class GetLoyaltyBalanceEndpoint(ILoyaltyService loyalty, ICurrentPartyResolver parties)
    : EndpointWithoutRequest<LoyaltyBalance>
{
    public override void Configure()
    {
        Get("/commerce/storefront/loyalty");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null) { await Send.NotFoundAsync(ct); return; }
        await Send.OkAsync(await loyalty.GetBalanceAsync(partyId.Value, ct), ct);
    }
}
