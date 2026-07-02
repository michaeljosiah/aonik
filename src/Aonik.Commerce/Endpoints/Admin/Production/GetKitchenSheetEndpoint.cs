using Aonik.Commerce.Contracts.Models.Production;
using Aonik.Commerce.Services.Production;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Production;

public class GetKitchenSheetEndpoint : EndpointWithoutRequest<KitchenSheetDto>
{
    private readonly IProductionOrderService _productionOrders;

    public GetKitchenSheetEndpoint(IProductionOrderService productionOrders) => _productionOrders = productionOrders;

    public override void Configure()
    {
        Get("/commerce/admin/production-orders/{id:guid}/kitchen-sheet");
        Policies("AdminUserPolicy");
        Summary(s => s.Summary =
            "The kitchen sheet (Spec 056 §11): per-dish prep detail (what to prep and how much, from each " +
            "line's frozen recipe snapshot) plus a merged all-ingredients totals bill — the printable " +
            "projection whose numbers are exactly what release consumes.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _productionOrders.GetKitchenSheetAsync(id, ct);
        if (result is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }
        await Send.OkAsync(result, ct);
    }
}
