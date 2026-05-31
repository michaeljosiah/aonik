using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Orders;

public class SubmitOrderEndpoint : EndpointWithoutRequest<BillPaymentOrderResponse>
{
    private readonly IOrderService _orderService;

    public SubmitOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Post("/orders/{orderId:guid}/submit");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Submit an order for processing";
            s.Description = "Submits a draft order for fulfilment, transitioning it from draft to submitted status.";
            s.Response(200, "Order submitted successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Order not found");
        });
        Options(x => x.WithTags("Orders"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.SubmitOrderAsync(orderId, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
