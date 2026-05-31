using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Orders;

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
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Remove a bill payment item";
            s.Description = "Removes a line item from a draft order.";
            s.Response(204, "Item removed successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Order or item not found");
        });
        Options(x => x.WithTags("Orders"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var orderItemId = Route<Guid>("orderItemId");
        await _orderService.RemoveBillPaymentItemAsync(orderId, orderItemId, ct);
        await Send.NoContentAsync(ct);
    }
}
