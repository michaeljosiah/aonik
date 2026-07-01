using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class GetStandardCostEndpoint : EndpointWithoutRequest<StandardCostDto>
{
    private readonly IProductCostingService _costing;

    public GetStandardCostEndpoint(IProductCostingService costing) => _costing = costing;

    public override void Configure()
    {
        Get("/commerce/admin/variants/{variantId:guid}/standard-cost");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Roll up the variant's standard cost (cost per portion) in ?currency= at ?at= (default now): the active recipe valued at date-aware ingredient costs, as a per-component breakdown plus total. A missing recipe or missing component cost is flagged, with the total withheld — never a silent zero.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var variantId = Route<Guid>("variantId");
        var currency = Query<string>("currency");
        var at = Query<DateTime?>("at", isRequired: false);

        var result = await _costing.RollupStandardCostAsync(variantId, currency!, at, ct);
        await Send.OkAsync(result, ct);
    }
}
