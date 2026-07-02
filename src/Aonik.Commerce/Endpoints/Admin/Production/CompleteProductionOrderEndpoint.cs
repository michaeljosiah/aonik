using Aonik.Commerce.Contracts.Api.Production;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class CompleteProductionOrderEndpoint : Endpoint<CompleteProductionOrderRequest, ProductionOrderDto>
{
    private readonly IProductionOrderService _productionOrders;

    public CompleteProductionOrderEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Post("/commerce/admin/production-orders/{id:guid}/complete");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary =
            "Complete a Released/InProgress production run (Spec 056 §10): records each line's produced " +
            "portions (explicit actuals, else the planned quantity) and — when yieldFinishedGoods is true, " +
            "the make-to-stock default — increments each produced variant's on-hand stock by that amount.");
    }

    public override async Task HandleAsync(CompleteProductionOrderRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var actuals = req.ActualQuantities?
            .Select(a => new ProducedQuantityLine(a.ProductionOrderLineId, a.ProducedQuantity))
            .ToList();
        var result = await _productionOrders.CompleteAsync(
            new CompleteProductionOrderCommand(id, actuals, req.YieldFinishedGoods), ct);
        await Send.OkAsync(result, ct);
    }
}
