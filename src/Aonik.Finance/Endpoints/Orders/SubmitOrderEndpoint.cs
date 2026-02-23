using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;

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
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.SubmitOrderAsync(orderId, ct);
        var response = OrderMapping.ToApiResponse(result);
        await Send.OkAsync(response, ct);
    }
}
