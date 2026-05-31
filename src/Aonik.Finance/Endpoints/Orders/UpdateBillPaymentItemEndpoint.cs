using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Update a bill payment item";
            s.Description = "Updates the details of an existing bill payment line item on a draft order.";
            s.Response(200, "Item updated successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Order or item not found");
        });
        Options(x => x.WithTags("Orders"));
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
