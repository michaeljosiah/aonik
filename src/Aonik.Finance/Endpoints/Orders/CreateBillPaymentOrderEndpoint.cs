using Aonik.Finance.Contracts.Api.Orders;
using Aonik.Finance.Contracts.Services.Orders;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

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
        Summary(s =>
        {
            s.Summary = "Create a bill payment order";
            s.Description = "Creates a new bill payment order with the specified payer, items, and currency. Supports an Idempotency-Key header.";
            s.Response(201, "Order created successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
        });
        Options(x => x.WithTags("Orders"));
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
