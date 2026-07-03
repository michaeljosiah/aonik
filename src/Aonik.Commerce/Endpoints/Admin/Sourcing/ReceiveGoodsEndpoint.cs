using Aonik.Commerce.Contracts.Api.Sourcing;
using Aonik.Commerce.Contracts.Models.Sourcing;
using Aonik.Commerce.Services.Sourcing;

using FastEndpoints;

namespace Aonik.Commerce.Endpoints.Admin.Sourcing;

public class ReceiveGoodsEndpoint : Endpoint<ReceiveGoodsRequest, GoodsReceiptDto>
{
    private readonly IGoodsReceiptService _goodsReceipts;

    public ReceiveGoodsEndpoint(IGoodsReceiptService goodsReceipts) => _goodsReceipts = goodsReceipts;

    public override void Configure()
    {
        Post("/commerce/admin/purchase-orders/{orderId:guid}/receipts");
        Policies("AdminUserWritePolicy");
        Summary(s => s.Summary = "Receive goods against a submitted purchase order (full or partial): increments ingredient on-hand, optionally refreshes the actual unit cost, resolves recovered low-stock alerts, and completes the order when fully received. Idempotent by the required IdempotencyKey — a retried key returns the existing receipt without double-counting.");
    }

    public override async Task HandleAsync(ReceiveGoodsRequest req, CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        // A body with Lines omitted binds null — project it to an empty list so the service's
        // "requires at least one line" validation produces the domain error, not an NRE here.
        var lines = req.Lines?
            .Select(l => new ReceiveGoodsLineCommand(l.IngredientId, l.QuantityReceived, l.UnitCostActual))
            .ToList() ?? [];
        var result = await _goodsReceipts.ReceiveAsync(
            new ReceiveGoodsCommand(
                orderId,
                req.IdempotencyKey,
                lines,
                req.ReceivedAt,
                req.Notes),
            ct);
        await Send.OkAsync(result, ct);
    }
}
