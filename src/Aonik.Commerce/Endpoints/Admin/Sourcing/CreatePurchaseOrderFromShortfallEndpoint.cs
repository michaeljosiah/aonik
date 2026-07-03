using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions.Ordering;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class CreatePurchaseOrderFromShortfallEndpoint : Endpoint<CreatePurchaseOrderFromShortfallRequest, OrderDto>
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public CreatePurchaseOrderFromShortfallEndpoint(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    public override void Configure()
    {
        Post("/commerce/admin/purchase-orders/from-shortfall");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Seed a Draft purchase order from low-stock alerts: named alert ids, or (omitted) every Open/Acknowledged alert this supplier can supply. Quantities are pack-rounded from the shortfall (or the level's reorder quantity); the source alerts flip to Ordered.");
    }

    public override async Task HandleAsync(CreatePurchaseOrderFromShortfallRequest req, CancellationToken ct)
    {
        var result = await _purchaseOrders.CreateFromShortfallAsync(
            new CreateFromShortfallCommand(req.SupplierId, req.AlertIds, req.Notes, req.IdempotencyKey), ct);
        await Send.OkAsync(result, ct);
    }
}
