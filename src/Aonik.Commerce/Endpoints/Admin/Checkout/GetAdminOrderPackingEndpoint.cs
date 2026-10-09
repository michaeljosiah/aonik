using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

public class GetAdminOrderPackingEndpoint(IAdminStorefrontService admin) : EndpointWithoutRequest<AdminOrderPackingDto>
{
    public override void Configure()
    {
        Get("/commerce/admin/orders/{orderId:guid}/packing");
        Policies("AdminReadPolicy");
        Summary(s => s.Summary = "Recipient packing slip for a confirmed paid storefront order.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var packing = await admin.GetOrderPackingAsync(Route<Guid>("orderId"), ct);
        if (packing is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(packing, ct);
    }
}
