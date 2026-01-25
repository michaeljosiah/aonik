using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class RemoveBillPaymentItemEndpoint : EndpointWithoutRequest
{
    private readonly IOrderService _orderService;

    public RemoveBillPaymentItemEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Delete("/orders/{orderId:guid}/items/{orderItemId:guid}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var orderItemId = Route<Guid>("orderItemId");
        await _orderService.RemoveBillPaymentItemAsync(orderId, orderItemId, ct);
        await Send.NoContentAsync(ct);
    }
}
