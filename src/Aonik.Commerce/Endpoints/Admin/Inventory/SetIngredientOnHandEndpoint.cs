using Aonik.Commerce.Contracts.Api.Inventory;
using Aonik.Commerce.Contracts.Models.Inventory;
using Aonik.Commerce.Services.Inventory;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Inventory;

public class SetIngredientOnHandEndpoint : Endpoint<SetOnHandRequest, StockLevelDto>
{
    private readonly IInventoryService _inventory;

    public SetIngredientOnHandEndpoint(IInventoryService inventory) => _inventory = inventory;

    public override void Configure()
    {
        Post("/commerce/admin/ingredients/{ingredientId:guid}/inventory");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set the on-hand stock for an ingredient (raw material), in its base unit.");
    }

    public override async Task HandleAsync(SetOnHandRequest req, CancellationToken ct)
    {
        var item = StockItemRef.Ingredient(Route<Guid>("ingredientId"));
        await _inventory.SetOnHandAsync(item, req.OnHand, ct);
        var level = await _inventory.GetStockLevelAsync(item, ct);
        await Send.OkAsync(level, ct);
    }
}
