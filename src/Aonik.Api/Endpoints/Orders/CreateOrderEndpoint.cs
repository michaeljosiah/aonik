using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Orders;

public class CreateOrderEndpoint : Endpoint<CreateOrderRequest, CreateOrderResponse>
{
    private readonly IOrderService _orderService;

    public CreateOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Post("/orders");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Orders.CreateOrderRequest(
            req.CustomerId,
            req.OrderType,
            req.ServiceCode,
            req.Amount,
            req.Currency,
            req.PricingQuoteId,
            req.ExchangeRate,
            req.RateMarkup,
            req.FeesTotal,
            req.TotalAmount,
            OrderMapping.ToApp(req.FeeBreakdown),
            OrderMapping.ToApp(req.Payer),
            OrderMapping.ToApp(req.Payee),
            OrderMapping.ToApp(req.Details),
            req.Items?.Select(OrderMapping.ToApp).ToList(),
            req.Metadata);

        var result = await _orderService.CreateAsync(appRequest, ct);

        var response = new CreateOrderResponse(
            result.OrderId,
            result.TenantId,
            result.OrderNumber,
            result.InvoiceId,
            result.Status,
            result.CreatedAt,
            result.PaymentStatus,
            result.InvoiceStatus);

        await Send.CreatedAtAsync<GetOrderEndpoint>(
            routeValues: new { orderId = response.OrderId },
            responseBody: response,
            cancellation: ct);
    }
}
