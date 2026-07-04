using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class SetIngredientCostEndpoint : Endpoint<SetIngredientCostRequest, IngredientCostDto>
{
    private readonly IIngredientCostService _costs;

    public SetIngredientCostEndpoint(IIngredientCostService costs) => _costs = costs;

    public override void Configure()
    {
        Put("/commerce/admin/ingredients/{ingredientId:guid}/cost");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set a new effective-dated unit cost for an ingredient (per its base unit, in one currency). Closes the prior cost and preserves it as history; omit effectiveFrom for now, or pass a future date to schedule the cost.");
    }

    public override async Task HandleAsync(SetIngredientCostRequest req, CancellationToken ct)
    {
        var ingredientId = Route<Guid>("ingredientId");
        var result = await _costs.SetCostAsync(
            new SetIngredientCostCommand(ingredientId, req.Currency, req.UnitCost, req.EffectiveFrom), ct);
        await Send.OkAsync(result, ct);
    }
}
