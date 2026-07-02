using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Planning;

public class GetPrepListEndpoint : EndpointWithoutRequest<PrepListDto>
{
    private readonly IProductionPlanningService _planning;

    public GetPrepListEndpoint(IProductionPlanningService planning) => _planning = planning;

    public override void Configure()
    {
        Get("/commerce/admin/planning/prep-list");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "The ingredient prep list: the production sheet for ?fromUtc=&toUtc= (UTC, half-open " +
            "[from, to)) exploded through active recipes, per-ingredient in base units. ?net=true " +
            "(default) nets each line against available stock (on-hand minus reserved) adding shortfall " +
            "and a suggested order quantity; ?net=false returns raw requirements.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var fromUtc = Query<DateTime>("fromUtc");
        var toUtc = Query<DateTime>("toUtc");
        var net = Query<bool?>("net", isRequired: false) ?? true;
        var result = await _planning.GetPrepListAsync(new ProductionWindow(fromUtc, toUtc), net, ct);
        await Send.OkAsync(result, ct);
    }
}
