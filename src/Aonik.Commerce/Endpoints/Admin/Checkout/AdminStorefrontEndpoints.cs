using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Services.Checkout;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Checkout;

// ─── Spec 083/081 dependency reads ──────────────────────────────────────────
// Tenant-wide storefront order projections and the carts admin read. All
// read-only; cart tokens are never serialized (R10); the cart detail's
// availability/price flags are computed against current state, never persisted.

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

/// <summary>GET /commerce/admin/parties/{partyId}/storefront — Spec 081's
/// Commerce tab: the party's storefront summary through the same party-scoped
/// queries the customer's own account uses; <c>adopted</c> is the recorded fact
/// only (no invented timestamps).</summary>
public class GetPartyStorefrontEndpoint : EndpointWithoutRequest<AdminPartyStorefrontDto>
{
    private readonly IAdminStorefrontService _admin;

    public GetPartyStorefrontEndpoint(IAdminStorefrontService admin) => _admin = admin;

    public override void Configure()
    {
        Get("/commerce/admin/parties/{partyId:guid}/storefront");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "A party's storefront summary: order history, active box cart, and the recorded adoption fact.");
    }

    public override async Task HandleAsync(CancellationToken ct)
        => await Send.OkAsync(await _admin.GetPartyStorefrontAsync(Route<Guid>("partyId"), ct), ct);
}
