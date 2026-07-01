using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class UpdateIngredientEndpoint : Endpoint<UpdateIngredientRequest, IngredientDto>
{
    private readonly IIngredientService _ingredients;

    public UpdateIngredientEndpoint(IIngredientService ingredients) => _ingredients = ingredients;

    public override void Configure()
    {
        Put("/commerce/admin/ingredients/{ingredientId:guid}");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Update an ingredient's master data (name, base unit, sku, category, notes, active flag).");
    }

    public override async Task HandleAsync(UpdateIngredientRequest req, CancellationToken ct)
    {
        var ingredientId = Route<Guid>("ingredientId");
        var result = await _ingredients.UpdateAsync(
            new UpdateIngredientCommand(ingredientId, req.Name, req.BaseUnit, req.Sku, req.Category, req.Notes, req.IsActive), ct);
        await Send.OkAsync(result, ct);
    }
}
