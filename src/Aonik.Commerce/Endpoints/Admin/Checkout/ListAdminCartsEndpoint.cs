using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

/// <summary>GET /commerce/admin/carts — Spec 083 dependency callout 2 (list).</summary>
public class ListAdminCartsEndpoint : EndpointWithoutRequest<PagedResult<AdminCartRowDto>>
{
    private readonly IAdminStorefrontService _admin;

    public ListAdminCartsEndpoint(IAdminStorefrontService admin) => _admin = admin;

    public override void Configure()
    {
        Get("/commerce/admin/carts");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Tenant-wide carts (box fullness included; tokens never serialized).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        var status = Query<string?>("status", isRequired: false);
        await Send.OkAsync(await _admin.ListCartsAsync(status, page, pageSize, ct), ct);
    }
}
