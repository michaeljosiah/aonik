using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Orders;

public class CreateBillPaymentOrderEndpoint : Endpoint<CreateBillPaymentOrderRequest, BillPaymentOrderResponse>
{
    private readonly IOrderService _orderService;

    public CreateBillPaymentOrderEndpoint(IOrderService orderService)
    {
        _orderService = orderService;
    }

    public override void Configure()
    {
        Post("/orders/bill-payments");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreateBillPaymentOrderRequest req, CancellationToken ct)
    {
        var idempotencyKey = HttpContext.Request.Headers.TryGetValue("Idempotency-Key", out var values)
            ? values.ToString()
            : null;

        var appRequest = OrderMapping.ToAppRequest(req, string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey);
        var result = await _orderService.CreateBillPaymentOrderAsync(appRequest, ct);
        var response = OrderMapping.ToApiResponse(result);

        await Send.CreatedAtAsync<GetOrderEndpoint>(
            routeValues: new { orderId = response.OrderId },
            responseBody: response,
            cancellation: ct);
    }
}
