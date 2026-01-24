using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class GetOrderEndpoint : EndpointWithoutRequest<OrderDetailResponse>
{
    private readonly IOrderService _orderService;

    public GetOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Get("/orders/{orderId}");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var orderId = Route<Guid>("orderId");
        var result = await _orderService.GetAsync(orderId, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        await Send.OkAsync(OrderMapping.ToApi(result), ct);
    }
}
