using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;
using Aonik.SharedKernel.Abstractions.Ordering;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class CreatePurchaseOrderEndpoint : Endpoint<CreatePurchaseOrderRequest, OrderDto>
{
    private readonly IPurchaseOrderService _purchaseOrders;

    public CreatePurchaseOrderEndpoint(IPurchaseOrderService purchaseOrders) => _purchaseOrders = purchaseOrders;

    public override void Configure()
    {
        Post("/commerce/admin/purchase-orders");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Create a Draft purchase order on the shared Order spine (OrderType PurchaseOrder). Line quantities are in the ingredient's base unit; omit a unit price to default from the supplier catalog (PackPrice / PackSize).");
    }

    public override async Task HandleAsync(CreatePurchaseOrderRequest req, CancellationToken ct)
    {
        var lines = (req.Lines ?? []).Select(l => new PurchaseOrderLineCommand(l.IngredientId, l.Quantity, l.UnitPrice)).ToList();
        var result = await _purchaseOrders.CreateAsync(
            new CreatePurchaseOrderCommand(req.SupplierId, lines, req.Currency, req.Notes, req.IdempotencyKey), ct);
        await Send.OkAsync(result, ct);
    }
}
