using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions.Ordering;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class CancelPurchaseOrderEndpoint : Endpoint<CancelPurchaseOrderRequest, OrderDto>
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public CancelPurchaseOrderEndpoint(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    public override void Configure()
    {
        Post("/commerce/admin/purchase-orders/{orderId:guid}/cancel");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Cancel a purchase order before receipt (allowed from Draft or Pending only).");
    }

    public override async Task HandleAsync(CancelPurchaseOrderRequest req, CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _purchaseOrders.CancelAsync(orderId, req.Reason, ct);
        await Send.OkAsync(result, ct);
    }
}
