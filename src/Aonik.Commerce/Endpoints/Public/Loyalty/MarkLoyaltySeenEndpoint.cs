using Aonik.Commerce.Contracts.Api.Loyalty;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Loyalty;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Loyalty;

public sealed class MarkLoyaltySeenEndpoint(ILoyaltyService loyalty, ICurrentPartyResolver parties)
    : Endpoint<MarkLoyaltySeenRequest>
{
    public override void Configure()
    {
        Post("/commerce/storefront/loyalty/seen");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(MarkLoyaltySeenRequest req, CancellationToken ct)
    {
        HttpContext.Response.Headers.CacheControl = "no-store";
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null) { await Send.NotFoundAsync(ct); return; }
        await loyalty.MarkSeenAsync(partyId.Value, req.Mark, ct);
        await Send.NoContentAsync(ct);
    }
}
