using Aonik.Commerce.Contracts.Api.Production;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class CancelProductionOrderEndpoint : Endpoint<CancelProductionOrderRequest, ProductionOrderDto>
{
    private readonly IProductionOrderService _productionOrders;

    public CancelProductionOrderEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Post("/commerce/admin/production-orders/{id:guid}/cancel");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary =
            "Cancel a Planned, Released, or InProgress production run (Spec 056 §8). Cancelling after " +
            "release does NOT auto-restore the consumed ingredient stock — reconcile via an explicit " +
            "stock adjustment if the materials went back on the shelf.");
    }

    public override async Task HandleAsync(CancelProductionOrderRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _productionOrders.CancelAsync(id, req.Reason, ct);
        await Send.OkAsync(result, ct);
    }
}
