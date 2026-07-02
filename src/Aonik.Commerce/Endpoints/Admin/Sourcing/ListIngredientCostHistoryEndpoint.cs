using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class ListIngredientCostHistoryEndpoint : EndpointWithoutRequest<IReadOnlyList<IngredientCostDto>>
{
    private readonly IIngredientCostService _costs;

    public ListIngredientCostHistoryEndpoint(IIngredientCostService costs) => _costs = costs;

    public override void Configure()
    {
        Get("/commerce/admin/ingredients/{ingredientId:guid}/cost/history");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List the ingredient's full reprice timeline, newest first, optionally filtered by ?currency=. Past costs are never overwritten — each reprice closes the prior row.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var ingredientId = Route<Guid>("ingredientId");
        var currency = Query<string?>("currency", isRequired: false);

        var result = await _costs.ListHistoryAsync(ingredientId, currency, ct);
        await Send.OkAsync(result, ct);
    }
}
