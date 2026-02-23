using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Models.Payments;
using Aonik.Finance.Contracts.Services.Payments;

namespace Aonik.Finance.Endpoints.Public.Payments;

public class GetPublicPaymentIntentStatusEndpoint : EndpointWithoutRequest<PublicPaymentIntentStatusResponse>
{
    private readonly IPublicPaymentService _publicPaymentService;

    public GetPublicPaymentIntentStatusEndpoint(IPublicPaymentService publicPaymentService)
    {
        _publicPaymentService = publicPaymentService;
    }

    public override void Configure()
    {
        Get("/public/payments/intents/status");
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

        var orderIdRaw = Query<string>("orderId", isRequired: false);
        if (!Guid.TryParse(orderIdRaw, out var orderId) || orderId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "orderId must be a valid UUID." }, ct);
            return;
        }

        Guid? paymentIntentId = null;
        var paymentIntentIdRaw = Query<string>("paymentIntentId", isRequired: false);
        if (!string.IsNullOrWhiteSpace(paymentIntentIdRaw))
        {
            if (!Guid.TryParse(paymentIntentIdRaw, out var parsedPaymentIntentId) || parsedPaymentIntentId == Guid.Empty)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await HttpContext.Response.WriteAsJsonAsync(new { error = "paymentIntentId must be a valid UUID when provided." }, ct);
                return;
            }

            paymentIntentId = parsedPaymentIntentId;
        }

        var providerReference = Query<string>("providerReference", isRequired: false);
        if (paymentIntentId == null && string.IsNullOrWhiteSpace(providerReference))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "Either paymentIntentId or providerReference is required." }, ct);
            return;
        }

        var result = await _publicPaymentService.GetGuestPaymentIntentStatusAsync(
            new GetGuestPaymentIntentStatusRequest(orderId, paymentIntentId, providerReference),
            ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var response = new PublicPaymentIntentStatusResponse(
            result.PaymentIntentId,
            result.OrderId,
            result.Amount,
            result.Currency,
            result.Status,
            result.ProviderReference,
            result.CreatedAt,
            result.OrderStatus);

        await Send.OkAsync(response, ct);
    }
}
