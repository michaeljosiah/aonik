using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Ordering;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class ListPurchaseOrdersEndpoint : EndpointWithoutRequest<PagedResult<OrderSummary>>
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public ListPurchaseOrdersEndpoint(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    public override void Configure()
    {
        Get("/commerce/admin/purchase-orders");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List purchase orders, newest first (optionally filtered by status: Draft, Pending, Complete, Cancelled).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = Query<string?>("status", isRequired: false);
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        var result = await _purchaseOrders.ListAsync(status, page, pageSize, ct);
        await Send.OkAsync(result, ct);
    }
}
