using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

/// <summary>GET /commerce/admin/orders — Spec 083 dependency callout 0: the
/// list projection carrying payment/fulfilment statuses and buyer kind.</summary>
public class ListAdminStorefrontOrdersEndpoint : EndpointWithoutRequest<PagedResult<AdminStorefrontOrderRowDto>>
{
    private readonly IAdminStorefrontService _admin;

    public ListAdminStorefrontOrdersEndpoint(IAdminStorefrontService admin) => _admin = admin;

    public override void Configure()
    {
        Get("/commerce/admin/orders");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Tenant-wide storefront orders with payment/fulfilment status and buyer kind, newest first.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        var paymentStatus = Query<string?>("paymentStatus", isRequired: false);
        await Send.OkAsync(await _admin.ListOrdersAsync(paymentStatus, page, pageSize, ct), ct);
    }
}
