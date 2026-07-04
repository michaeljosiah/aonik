using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class StartProductionOrderEndpoint : EndpointWithoutRequest<ProductionOrderDto>
{
    private readonly IProductionOrderService _productionOrders;

    public StartProductionOrderEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Post("/commerce/admin/production-orders/{id:guid}/start");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary =
            "Mark a Released production run as InProgress (the kitchen is cooking) — an optional " +
            "operational sub-state with no stock effect (Spec 056 §8).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _productionOrders.StartAsync(id, ct);
        await Send.OkAsync(result, ct);
    }
}
