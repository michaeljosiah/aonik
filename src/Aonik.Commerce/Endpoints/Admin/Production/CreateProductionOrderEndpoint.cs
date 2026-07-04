using Aonik.Commerce.Contracts.Api.Production;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class CreateProductionOrderEndpoint : Endpoint<CreateProductionOrderRequest, ProductionOrderDto>
{
    private readonly IProductionOrderService _productionOrders;

    public CreateProductionOrderEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Post("/commerce/admin/production-orders");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary =
            "Create a Planned production run (Spec 056): the dishes (variants) and portions to make. " +
            "Each line's recipe is exploded once and frozen onto the line as its per-portion snapshot — " +
            "a variant without an active recipe rejects the create. No stock moves until release.");
    }

    public override async Task HandleAsync(CreateProductionOrderRequest req, CancellationToken ct)
    {
        // A body with Lines omitted binds null — project it to an empty list so the service's
        // "requires at least one line" validation produces the domain error, not an NRE here.
        var lines = req.Lines?
            .Select(l => new ProductionOrderLineCommand(l.ProductVariantId, l.PlannedQuantity))
            .ToList() ?? [];
        var result = await _productionOrders.CreateAsync(
            new CreateProductionOrderCommand(req.PlannedFor, lines, req.Notes), ct);
        await Send.OkAsync(result, ct);
    }
}
