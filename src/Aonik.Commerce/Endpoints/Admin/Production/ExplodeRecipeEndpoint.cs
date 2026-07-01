using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class ExplodeRecipeEndpoint : EndpointWithoutRequest<RecipeExplosionDto>
{
    private readonly IRecipeService _recipes;

    public ExplodeRecipeEndpoint(IRecipeService recipes) => _recipes = recipes;

    public override void Configure()
    {
        Get("/commerce/admin/variants/{variantId:guid}/recipe/explosion");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Explode the variant's active recipe into required ingredient quantities for ?portions=N.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var variantId = Route<Guid>("variantId");
        var portions = Query<decimal>("portions");
        var result = await _recipes.ExplodeAsync(variantId, portions, ct);
        await Send.OkAsync(result, ct);
    }
}
