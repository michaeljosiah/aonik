using Aonik.Commerce.Contracts.Api.Production;
using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class SetRecipeEndpoint : Endpoint<SetRecipeRequest, RecipeDto>
{
    private readonly IRecipeService _recipes;

    public SetRecipeEndpoint(IRecipeService recipes) => _recipes = recipes;

    public override void Configure()
    {
        Put("/commerce/admin/variants/{variantId:guid}/recipe");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Define (or replace, in place) the active recipe / bill of materials for a variant.");
    }

    public override async Task HandleAsync(SetRecipeRequest req, CancellationToken ct)
    {
        var variantId = Route<Guid>("variantId");
        var command = new SetRecipeCommand(
            variantId,
            req.Name,
            req.YieldQuantity,
            req.YieldUnit,
            req.Components?.Select(c => new RecipeComponentCommand(c.IngredientId, c.Quantity, c.Notes)).ToList()
                ?? new List<RecipeComponentCommand>());

        var result = await _recipes.SetRecipeAsync(command, ct);
        await Send.OkAsync(result, ct);
    }
}
