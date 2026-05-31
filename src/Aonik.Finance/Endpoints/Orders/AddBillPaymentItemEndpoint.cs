using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Orders;

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
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Add a bill payment item to an order";
            s.Description = "Adds a new bill payment line item to an existing draft order.";
            s.Response(201, "Item added successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Order not found");
        });
        Options(x => x.WithTags("Orders"));
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
