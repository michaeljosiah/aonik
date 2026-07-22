using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Public.Checkout;

/// <summary>Spec 072 Y5 — the customer's own orders, paged (`page`/`pageSize`, newest first).
/// Party-scoped at the query (Z5): another party's order id is a 404, never a 403 oracle.
/// Admits PersonalUser (Z6).</summary>
public class ListMyOrdersEndpoint : EndpointWithoutRequest<Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>>
{
    private readonly IStorefrontOrderService _orders;
    private readonly ICurrentPartyResolver _parties;

    public ListMyOrdersEndpoint(IStorefrontOrderService orders, ICurrentPartyResolver parties)
    {
        _orders = orders;
        _parties = parties;
    }

    public override void Configure()
    {
        Get("/commerce/storefront/orders");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "The authenticated customer's own orders.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        var partyId = await _parties.GetCurrentPartyIdAsync(ct);
        if (partyId is null)
        {
            // An unlinked principal simply has no orders.
            await Send.OkAsync(new Contracts.Models.Catalog.PagedResult<StorefrontOrderSummaryDto>([], 0, page, pageSize), ct);
            return;
        }
        await Send.OkAsync(await _orders.ListMyOrdersAsync(partyId.Value, page, pageSize, ct), ct);
    }
}

public class GetMyOrderEndpoint : EndpointWithoutRequest<StorefrontOrderDetailDto>
{
    private readonly IStorefrontOrderService _orders;
    private readonly ICurrentPartyResolver _parties;

    public GetMyOrderEndpoint(IStorefrontOrderService orders, ICurrentPartyResolver parties)
    {
        _orders = orders;
        _parties = parties;
    }

    public override void Configure()
    {
        Get("/commerce/storefront/orders/{orderId:guid}");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "One of the authenticated customer's orders, with items and box selections.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var partyId = await _parties.GetCurrentPartyIdAsync(ct);
        var detail = partyId is null
            ? null
            : await _orders.GetMyOrderAsync(partyId.Value, Route<Guid>("orderId"), ct);
        if (detail is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(detail, ct);
    }
}
