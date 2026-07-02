using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class GetRecipeEndpoint : EndpointWithoutRequest<RecipeDto>
{
    private readonly IRecipeService _recipes;

    public GetRecipeEndpoint(IRecipeService recipes) => _recipes = recipes;

    public override void Configure()
    {
        Get("/commerce/admin/variants/{variantId:guid}/recipe");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get the variant's active recipe (bill of materials) with its ingredient components.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var variantId = Route<Guid>("variantId");
        var result = await _recipes.GetRecipeAsync(variantId, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
