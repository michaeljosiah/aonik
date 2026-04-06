using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Orders;

public class CancelOrderEndpoint : Endpoint<CancelOrderRequest, BillPaymentOrderResponse>
{
    private readonly IOrderService _orderService;

    public CancelOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Post("/orders/{orderId:guid}/cancel");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Cancel an order";
            s.Description = "Cancels an existing order with a reason. Only orders in a cancellable state can be cancelled.";
            s.Response(200, "Order cancelled successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Order not found");
        });
        Options(x => x.WithTags("Orders"));
    }

    public override async Task HandleAsync(CancelOrderRequest req, CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.CancelOrderAsync(orderId, req.Reason, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
