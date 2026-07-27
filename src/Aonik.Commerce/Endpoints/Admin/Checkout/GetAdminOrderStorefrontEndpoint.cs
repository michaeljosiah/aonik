using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

/// <summary>GET /commerce/admin/orders/{orderId}/storefront — Spec 083
/// dependency callout 1: the ENTIRE storefront order detail (items with
/// add-on/delivery markers, the kitchen landing, the charge envelope).</summary>
public class GetAdminOrderStorefrontEndpoint : EndpointWithoutRequest<AdminOrderStorefrontDto>
{
    private readonly IAdminStorefrontService _admin;

    public GetAdminOrderStorefrontEndpoint(IAdminStorefrontService admin) => _admin = admin;

    public override void Configure()
    {
        Get("/commerce/admin/orders/{orderId:guid}/storefront");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The full storefront detail of one order — the spine's generic read is bill-payment-shaped.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var detail = await _admin.GetOrderStorefrontAsync(Route<Guid>("orderId"), ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(detail, ct);
    }
}
