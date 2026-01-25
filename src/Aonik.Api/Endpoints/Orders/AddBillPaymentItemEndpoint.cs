using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class AddBillPaymentItemEndpoint : Endpoint<CreateBillPaymentItemRequest, OrderItemResponse>
{
    private readonly IOrderService _orderService;

    public AddBillPaymentItemEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Post("/orders/{orderId:guid}/items/bill-payments");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateBillPaymentItemRequest req, CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var appRequest = OrderMapping.ToAppRequest(req);
        var result = await _orderService.AddBillPaymentItemAsync(orderId, appRequest, ct);
        var response = OrderMapping.ToApiResponse(result);

        await Send.CreatedAtAsync<GetOrderEndpoint>(
            routeValues: new { orderId },
            responseBody: response,
            cancellation: ct);
    }
}
