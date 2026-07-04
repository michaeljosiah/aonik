using Aonik.Commerce.Contracts.Api.Inventory;
using Aonik.Commerce.Services.Inventory;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Inventory;

public class SetOnHandEndpoint : Endpoint<SetOnHandRequest, InventoryAvailabilityResponse>
{
    private readonly IInventoryService _inventory;

    public SetOnHandEndpoint(IInventoryService inventory) => _inventory = inventory;

    public override void Configure()
    {
        Post("/commerce/admin/variants/{variantId:guid}/inventory");
        Policies("AdminWritePolicy");
        Summary(s => s.Summary = "Set the on-hand stock for a variant.");
    }

    public override async Task HandleAsync(SetOnHandRequest req, CancellationToken ct)
    {
        var variantId = Route<Guid>("variantId");
        await _inventory.SetOnHandAsync(variantId, req.OnHand, ct);
        var available = await _inventory.GetAvailableAsync(variantId, ct);
        await Send.OkAsync(new InventoryAvailabilityResponse(variantId, available), ct);
    }
}
