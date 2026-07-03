using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Planning;

public class GetProductionSheetEndpoint : EndpointWithoutRequest<ProductionSheetDto>
{
    private readonly IProductionPlanningService _planning;

    public GetProductionSheetEndpoint(IProductionPlanningService planning) => _planning = planning;

    public override void Configure()
    {
        Get("/commerce/admin/planning/production-sheet");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "The aggregated production sheet: per-variant portion demand from committed product-purchase " +
            "orders created in ?fromUtc=&toUtc= (UTC, half-open [from, to)). Bundle lines are expanded " +
            "into their chosen component variants.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fromUtc = Query<DateTime>("fromUtc");
        var toUtc = Query<DateTime>("toUtc");
        var result = await _planning.GetProductionSheetAsync(new ProductionWindow(fromUtc, toUtc), ct);
        await Send.OkAsync(result, ct);
    }
}
