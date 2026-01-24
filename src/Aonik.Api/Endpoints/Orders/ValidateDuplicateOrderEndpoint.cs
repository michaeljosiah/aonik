using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class ValidateDuplicateOrderEndpoint : Endpoint<ValidateDuplicateOrderRequest, ValidateDuplicateOrderResponse>
{
    private readonly IOrderService _orderService;

    public ValidateDuplicateOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Post("/orders/validate-duplicate");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(ValidateDuplicateOrderRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Orders.ValidateDuplicateOrderRequest(
            req.CustomerId,
            req.OrderType,
            req.ServiceCode,
            req.Amount,
            req.Currency,
            OrderMapping.ToApp(req.Details),
            req.RequestedAt);

        var result = await _orderService.ValidateDuplicateAsync(appRequest, ct);

        var response = new ValidateDuplicateOrderResponse(
            result.OrderId,
            result.TenantId,
            result.OrderNumber,
            result.InvoiceId,
            result.Status,
            result.CreatedAt);

        await Send.OkAsync(response, ct);
    }
}
