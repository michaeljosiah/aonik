using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Api.Contracts.Orders;
using Aonik.Application.Services.Orders;

namespace Aonik.Api.Endpoints.Public.Orders;

public class CreatePublicBillPaymentDraftEndpoint : Endpoint<CreateGuestBillPaymentDraftRequest, GuestBillPaymentDraftResponse>
{
    private readonly IPublicOrderService _publicOrderService;

    public CreatePublicBillPaymentDraftEndpoint(IPublicOrderService publicOrderService)
    {
        _publicOrderService = publicOrderService;
    }

    public override void Configure()
    {
        Post("/public/orders/bill-payments/drafts");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateGuestBillPaymentDraftRequest req, CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        if (req.BillerId == Guid.Empty || req.ServiceId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "billerId and serviceId must be valid UUIDs." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.ServiceCode)
            || string.IsNullOrWhiteSpace(req.ServiceName)
            || string.IsNullOrWhiteSpace(req.CountryCode)
            || string.IsNullOrWhiteSpace(req.Currency))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "serviceCode, serviceName, countryCode, and currency are required." }, ct);
            return;
        }

        var appRequest = new Application.Models.Orders.CreateGuestBillPaymentDraftRequest(
            req.BillerId,
            req.ServiceId,
            req.ServiceCode,
            req.ServiceName,
            req.BillerName,
            req.CountryCode,
            req.Currency,
            req.ServiceFieldValues,
            req.IsValidated,
            req.CapturedAt,
            req.ValidationMode,
            req.AccountHolderName,
            req.RequestedAmount,
            req.Channel);

        var result = await _publicOrderService.CreateGuestBillPaymentDraftAsync(appRequest, ct);
        var response = new GuestBillPaymentDraftResponse(result.OrderId, result.Status, result.CreatedAt);

        await Send.OkAsync(response, ct);
    }
}
