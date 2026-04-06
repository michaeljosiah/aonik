using FastEndpoints;
using Microsoft.AspNetCore.Http;

using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;

namespace Aonik.Finance.Endpoints.Public.Payments;

public class CreatePublicPaymentIntentEndpoint : Endpoint<CreatePublicPaymentIntentRequest, PublicPaymentIntentResponse>
{
    private readonly IPublicPaymentService _publicPaymentService;

    public CreatePublicPaymentIntentEndpoint(IPublicPaymentService publicPaymentService)
    {
        _publicPaymentService = publicPaymentService;
    }

    public override void Configure()
    {
        Post("/public/payments/intents");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create payment intent (public)";
            s.Description = "Creates a guest payment intent for an existing draft order. No authentication required.";
            s.Response(200, "Success");
            s.Response(400, "Invalid request");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CreatePublicPaymentIntentRequest req, CancellationToken ct)
    {
        var tenantHeader = HttpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantHeader) || !Guid.TryParse(tenantHeader, out _))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "X-Tenant-Id header is required." }, ct);
            return;
        }

        if (req.OrderId == Guid.Empty)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "orderId must be a valid UUID." }, ct);
            return;
        }

        if (string.IsNullOrWhiteSpace(req.Provider) || string.IsNullOrWhiteSpace(req.PaymentMethodType))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsJsonAsync(new { error = "provider and paymentMethodType are required." }, ct);
            return;
        }

        var result = await _publicPaymentService.CreateGuestPaymentIntentAsync(
            new Finance.Contracts.Models.Payments.CreateGuestPaymentIntentRequest(
                req.OrderId,
                req.Provider,
                req.PaymentMethodType,
                req.ReturnUrl,
                req.CancelUrl),
            ct);

        var response = new PublicPaymentIntentResponse(
            result.PaymentIntentId,
            result.OrderId,
            result.Amount,
            result.Currency,
            result.Status,
            result.Provider,
            result.ProviderReference,
            result.ClientSecret,
            result.CheckoutUrl,
            result.CreatedAt);

        await Send.OkAsync(response, ct);
    }
}
