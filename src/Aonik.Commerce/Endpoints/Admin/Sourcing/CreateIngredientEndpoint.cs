using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class CreateIngredientEndpoint : Endpoint<CreateIngredientRequest, IngredientDto>
{
    private readonly IIngredientService _ingredients;

    public CreateIngredientEndpoint(IIngredientService ingredients) => _ingredients = ingredients;

    public override void Configure()
    {
        Post("/commerce/admin/ingredients");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Create an ingredient (raw material) with a base unit of measure.");
    }

    public override async Task HandleAsync(CreateIngredientRequest req, CancellationToken ct)
    {
        var result = await _ingredients.CreateAsync(
            new CreateIngredientCommand(req.Name, req.BaseUnit, req.Sku, req.Category, req.Notes), ct);
        await Send.OkAsync(result, ct);
    }
}
