using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class ListProductionOrdersEndpoint : EndpointWithoutRequest<IReadOnlyList<ProductionOrderDto>>
{
    private readonly IProductionOrderService _productionOrders;

    public ListProductionOrdersEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Get("/commerce/admin/production-orders");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "List the tenant's production runs, most recent planned-for first; optionally filter with " +
            "?status= (Planned, Released, InProgress, Completed, Cancelled).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = Query<string?>("status", isRequired: false);
        var result = await _productionOrders.ListAsync(status, ct);
        await Send.OkAsync(result, ct);
    }
}
