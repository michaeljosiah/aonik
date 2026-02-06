using ApiContracts = Aonik.Api.Contracts.Orders;
using Aonik.Application.Models.Identity;
using AppModels = Aonik.Application.Models.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class ListOrdersEndpoint : Endpoint<ApiContracts.ListOrdersRequest, PagedResult<ApiContracts.OrderListItemResponse>>
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

    public override async Task HandleAsync(ApiContracts.ListOrdersRequest req, CancellationToken ct)
    {
        var result = await _orderService.ListOrdersAsync(
            new AppModels.ListOrdersRequest(
                req.PageNumber,
                req.PageSize,
                req.Status,
                req.OrderType,
                req.Search),
            ct);

        var response = new PagedResult<ApiContracts.OrderListItemResponse>(
            result.Items.Select(item => new ApiContracts.OrderListItemResponse(
                item.OrderId,
                item.OrderType,
                item.Status,
                item.PayerPartyId,
                item.PayerName,
                item.OriginCountry,
                item.OriginCurrency,
                item.TotalAmountIn,
                item.TotalAmountOut,
                item.DestinationCurrency,
                item.CreatedAt,
                item.UpdatedAt)).ToList(),
            result.TotalCount,
            result.PageNumber,
            result.PageSize);

        await Send.OkAsync(response, ct);
    }
}
