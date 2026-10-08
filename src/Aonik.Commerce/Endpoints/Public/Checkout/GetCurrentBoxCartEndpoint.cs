using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class GetCurrentBoxCartEndpoint(IBoxCartService boxCarts, ICurrentPartyResolver parties)
    : EndpointWithoutRequest<BoxCartDto>
{
    public override void Configure()
    {
        Get("/commerce/carts/box/current");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get the signed-in customer's current open box without creating one.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        var box = partyId.HasValue ? await boxCarts.GetCurrentAsync(partyId.Value, ct) : null;
        if (box is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(box, ct);
    }
}
