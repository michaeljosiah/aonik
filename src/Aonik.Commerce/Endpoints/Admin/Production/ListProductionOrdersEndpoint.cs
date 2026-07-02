using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;
using Aonik.SharedKernel.Abstractions;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class ListProductionOrdersEndpoint : EndpointWithoutRequest<PagedResult<ProductionOrderSummaryDto>>
{
    private readonly IProductionOrderService _productionOrders;

    public ListProductionOrdersEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Get("/commerce/admin/production-orders");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "List the tenant's production runs as paged summary rows, most recent planned-for first; optionally " +
            "filter with ?status= (Planned, Released, InProgress, Completed, Cancelled). Per-line detail is on " +
            "the kitchen sheet.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var status = Query<string?>("status", isRequired: false);
        var page = Query<int?>("page", isRequired: false) ?? 1;
        var pageSize = Query<int?>("pageSize", isRequired: false) ?? 20;
        var result = await _productionOrders.ListAsync(status, page, pageSize, ct);
        await Send.OkAsync(result, ct);
    }
}
