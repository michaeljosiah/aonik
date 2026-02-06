using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;

namespace Aonik.Api.Endpoints.Public.Orders;

public class GetPublicBillPaymentDraftEndpoint : EndpointWithoutRequest<GuestBillPaymentDraftDetailResponse>
{
    private readonly IPublicOrderService _publicOrderService;

    public GetPublicBillPaymentDraftEndpoint(IPublicOrderService publicOrderService)
    {
        _publicOrderService = publicOrderService;
    }

    public override void Configure()
    {
        Get("/public/orders/bill-payments/drafts/{orderId}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        var orderId = Route<Guid>("orderId");
        if (orderId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "orderId must be a valid UUID." }, ct);
            return;
        }

        var result = await _publicOrderService.GetGuestBillPaymentDraftAsync(orderId, ct);
        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new GuestBillPaymentDraftDetailResponse(
            result.OrderId,
            result.Status,
            result.CreatedAt,
            result.CountryCode,
            result.Currency,
            result.BillerId,
            result.BillerName,
            result.ServiceId,
            result.ServiceCode,
            result.ServiceName,
            result.ServiceFieldValues,
            result.IsValidated,
            result.CapturedAt,
            result.ValidationMode,
            result.AccountHolderName,
            result.RequestedAmount,
            result.Channel);

        await Send.OkAsync(response, ct);
    }
}
