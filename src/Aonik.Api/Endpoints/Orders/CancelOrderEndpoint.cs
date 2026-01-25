using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

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
    }

    public override async Task HandleAsync(CancelOrderRequest req, CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.CancelOrderAsync(orderId, req.Reason, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
