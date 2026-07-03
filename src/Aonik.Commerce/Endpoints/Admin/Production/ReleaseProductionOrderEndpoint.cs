using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class ReleaseProductionOrderEndpoint : EndpointWithoutRequest<ProductionOrderDto>
{
    private readonly IProductionOrderService _productionOrders;

    public ReleaseProductionOrderEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Post("/commerce/admin/production-orders/{id:guid}/release");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary =
            "Release a Planned production run (Spec 056 §9): CONSUMES ingredient stock — the frozen per-line " +
            "recipe snapshots merged into one bill and drawn down all-or-nothing in a single commit. " +
            "Fails fast (nothing consumed) if any ingredient's available stock is short. " +
            "Re-releasing a Released run is a no-op; stock is never double-consumed.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _productionOrders.ReleaseAsync(id, ct);
        await Send.OkAsync(result, ct);
    }
}
