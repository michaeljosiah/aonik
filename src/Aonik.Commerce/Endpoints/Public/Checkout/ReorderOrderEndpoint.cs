using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

public sealed class ReorderOrderEndpoint(IOrderReorderService reorder, ICurrentPartyResolver parties)
    : EndpointWithoutRequest<BoxCartDto>
{
    public override void Configure()
    {
        Post("/commerce/storefront/orders/{orderId:guid}/reorder");
        Policies("AdminUserPolicy");
        Summary(summary => summary.Summary = "Start a fresh box from your purchased dishes using today's catalogue.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = await parties.GetCurrentPartyIdAsync(ct);
        var box = partyId is { } party ? await reorder.ReorderAsync(Route<Guid>("orderId"), party, ct) : null;
        if (box is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(box, ct);
    }
}
