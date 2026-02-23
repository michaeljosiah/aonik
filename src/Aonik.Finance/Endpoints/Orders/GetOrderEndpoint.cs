using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;

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
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.GetOrderAsync(orderId, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
