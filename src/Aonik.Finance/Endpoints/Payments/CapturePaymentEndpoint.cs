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
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Capture a payment intent";
            s.Description = "Captures an authorized payment intent, completing the payment transaction.";
            s.Response(200, "Payment captured successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment intent not found");
            s.Response(422, "Payment cannot be captured (wrong state, or no resolved payer / payment method)");
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
        catch (InvalidOperationException ex)
        {
            // Not-found → 404; wrong-state or an unresolved payer/method (the money-movement
            // guard) → 422, matching the Billing endpoints' convention.
            AddError(ex.Message);
            await Send.ErrorsAsync(ex.Message.Contains("not found") ? 404 : 422, ct);
        }
    }
}
