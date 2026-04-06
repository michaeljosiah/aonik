using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

public class CapturePaymentEndpoint : EndpointWithoutRequest<PaymentIntentResponse>
{
    private readonly IPaymentService _paymentService;

    public CapturePaymentEndpoint(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public override void Configure()
    {
        Post("/payments/intents/{id}/capture");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Capture a payment intent";
            s.Description = "Captures an authorized payment intent, completing the payment transaction.";
            s.Response(200, "Payment captured successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment intent not found");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var result = await _paymentService.CapturePaymentAsync(id, ct);

            var response = new PaymentIntentResponse(
                result.Id,
                result.OrderId,
                result.InvoiceId,
                result.Amount,
                result.Currency,
                result.Status.ToString(),
                result.Reference,
                result.CreatedUtc);

            await Send.OkAsync(response, ct);
        }
        catch (InvalidOperationException)
        {
            await Send.NotFoundAsync(ct);
        }
    }
}
