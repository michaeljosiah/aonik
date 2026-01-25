using Aonik.Api.Contracts.Payments;
using Aonik.Application.Services.Payments;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Payments;

public class CreatePaymentIntentEndpoint : Endpoint<CreatePaymentIntentRequest, PaymentIntentResponse>
{
    private readonly IPaymentService _paymentService;

    public CreatePaymentIntentEndpoint(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public override void Configure()
    {
        Post("/payments/intents");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CreatePaymentIntentRequest req, CancellationToken ct)
    {
        var appRequest = new Application.Models.Payments.CreatePaymentIntentRequest(
            req.Amount,
            req.Currency,
            req.Reference,
            req.OrderId,
            req.InvoiceId);

        var result = await _paymentService.CreatePaymentIntentAsync(appRequest, ct);

        var response = new PaymentIntentResponse(
            result.Id,
            result.OrderId,
            result.InvoiceId,
            result.Amount,
            result.Currency,
            result.Status.ToString(),
            result.Reference,
            result.CreatedUtc);

        await Send.CreatedAtAsync<GetPaymentIntentEndpoint>(
            routeValues: new { id = response.Id },
            responseBody: response,
            cancellation: ct);
    }
}
