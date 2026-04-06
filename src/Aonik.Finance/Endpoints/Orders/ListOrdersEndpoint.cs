using ApiContracts = Aonik.Finance.Contracts.Api.Orders;
using Aonik.SharedKernel.Abstractions;
using AppModels = Aonik.Finance.Contracts.Models.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Orders;

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
        Summary(s =>
        {
            s.Summary = "List orders";
            s.Description = "Returns a paginated list of orders, with optional filtering by status, order type, and search term.";
            s.Response(200, "Orders retrieved successfully");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Orders"));
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
