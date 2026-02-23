using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Orders;

public class UpdateBillPaymentItemEndpoint : Endpoint<UpdateBillPaymentItemRequest, OrderItemResponse>
{
    private readonly IOrderService _orderService;

    public UpdateBillPaymentItemEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Put("/orders/{orderId:guid}/items/{orderItemId:guid}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(UpdateBillPaymentItemRequest req, CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var orderItemId = Route<Guid>("orderItemId");
        var appRequest = OrderMapping.ToAppRequest(req);
        var result = await _orderService.UpdateBillPaymentItemAsync(orderId, orderItemId, appRequest, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
