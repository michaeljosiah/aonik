using Aonik.Commerce.Contracts.Api.Inventory;
using Aonik.Commerce.Contracts.Models.Inventory;
using Aonik.Commerce.Services.Inventory;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Inventory;

public class SetIngredientReorderPointEndpoint : Endpoint<SetReorderPointRequest, StockLevelDto>
{
    private readonly IInventoryService _inventory;

    public SetIngredientReorderPointEndpoint(IInventoryService inventory) => _inventory = inventory;

    public override void Configure()
    {
        Put("/commerce/admin/ingredients/{ingredientId:guid}/reorder-point");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set an ingredient's reorder point (low-stock alert threshold on available stock) and optional suggested reorder quantity. Null reorderPoint clears alerting.");
    }

    public override async Task HandleAsync(SetReorderPointRequest req, CancellationToken ct)
    {
        var item = StockItemRef.Ingredient(Route<Guid>("ingredientId"));
        var level = await _inventory.SetReorderPointAsync(item, req.ReorderPoint, req.ReorderQuantity, ct);
        await Send.OkAsync(level, ct);
    }
}
