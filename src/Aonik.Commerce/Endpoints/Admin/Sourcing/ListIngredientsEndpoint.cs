using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class ListIngredientsEndpoint : EndpointWithoutRequest<IReadOnlyList<IngredientDto>>
{
    private readonly IIngredientService _ingredients;

    public ListIngredientsEndpoint(IIngredientService ingredients) => _ingredients = ingredients;

    public override void Configure()
    {
        Get("/commerce/admin/ingredients");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "List the tenant's ingredients (active only unless includeInactive=true).");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var includeInactive = Query<bool?>("includeInactive", isRequired: false) ?? false;
        var result = await _ingredients.ListAsync(includeInactive, ct);
        await Send.OkAsync(result, ct);
    }
}
