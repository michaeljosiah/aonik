using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class GetIngredientCostEndpoint : EndpointWithoutRequest<IngredientCostDto>
{
    private readonly IIngredientCostService _costs;

    public GetIngredientCostEndpoint(IIngredientCostService costs) => _costs = costs;

    public override void Configure()
    {
        Get("/commerce/admin/ingredients/{ingredientId:guid}/cost");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get the ingredient's current unit cost in ?currency= — date-aware: the cost effective at ?at= (default now); a scheduled future cost never prices early. 404 when no cost is effective.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ingredientId = Route<Guid>("ingredientId");
        var currency = Query<string>("currency");
        var at = Query<DateTime?>("at", isRequired: false);

        var result = await _costs.GetCurrentCostAsync(ingredientId, currency!, at, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
