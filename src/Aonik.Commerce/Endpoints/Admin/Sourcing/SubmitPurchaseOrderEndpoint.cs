using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions.Ordering;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class SubmitPurchaseOrderEndpoint : EndpointWithoutRequest<OrderDto>
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public SubmitPurchaseOrderEndpoint(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    public override void Configure()
    {
        Post("/commerce/admin/purchase-orders/{orderId:guid}/submit");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Submit a Draft purchase order to the supplier (Draft -> Pending on the spine's existing status codes).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _purchaseOrders.SubmitAsync(orderId, ct);
        await Send.OkAsync(result, ct);
    }
}
