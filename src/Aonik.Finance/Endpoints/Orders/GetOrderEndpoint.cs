using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Orders;

public class GetOrderEndpoint : EndpointWithoutRequest<BillPaymentOrderResponse>
{
    private readonly IOrderService _orderService;

    public GetOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Get("/orders/{orderId:guid}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get an order by ID";
            s.Description = "Retrieves the full details of a bill payment order, including its line items.";
            s.Response(200, "Order retrieved successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Order not found");
        });
        Options(x => x.WithTags("Orders"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.GetOrderAsync(orderId, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
