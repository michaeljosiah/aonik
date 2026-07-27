using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

/// <summary>GET /commerce/admin/carts/{cartId} — Spec 083 dependency callout 2
/// (detail): lines with nested personalisation plus read-only computed
/// availability/price flags and the order link when checked out.</summary>
public class GetAdminCartEndpoint : EndpointWithoutRequest<AdminCartDetailDto>
{
    private readonly IAdminStorefrontService _admin;

    public GetAdminCartEndpoint(IAdminStorefrontService admin) => _admin = admin;

    public override void Configure()
    {
        Get("/commerce/admin/carts/{cartId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "One cart's admin detail — computed flags are read-only; drift repair stays the customer load path's job.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var detail = await _admin.GetCartAsync(Route<Guid>("cartId"), ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(detail, ct);
    }
}
