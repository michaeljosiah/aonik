using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class ListOrdersEndpoint : Endpoint<OrderListRequest, OrderListResponse>
{
    private readonly IOrderService _orderService;

    public ListOrdersEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Get("/orders");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(OrderListRequest req, CancellationToken ct)
    {
        var appQuery = new Application.Models.Orders.OrderListQuery(
            req.CustomerId,
            req.Status,
            req.OrderType,
            req.ServiceCode,
            req.DateFrom,
            req.DateTo,
            req.PageNumber,
            req.PageSize);

        var result = await _orderService.ListAsync(appQuery, ct);
        await Send.OkAsync(OrderMapping.ToApi(result), ct);
    }
}
