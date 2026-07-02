using Aonik.Commerce.Contracts.Models.Inventory;
using Aonik.Commerce.Services.Inventory;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Inventory;

public class GetIngredientInventoryEndpoint : EndpointWithoutRequest<StockLevelDto>
{
    private readonly IInventoryService _inventory;

    public GetIngredientInventoryEndpoint(IInventoryService inventory) => _inventory = inventory;

    public override void Configure()
    {
        Get("/commerce/admin/ingredients/{ingredientId:guid}/inventory");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary = "Get an ingredient's stock level: on-hand, reserved, available, and reorder point/quantity.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var item = StockItemRef.Ingredient(Route<Guid>("ingredientId"));
        var level = await _inventory.GetStockLevelAsync(item, ct);
        await Send.OkAsync(level, ct);
    }
}
