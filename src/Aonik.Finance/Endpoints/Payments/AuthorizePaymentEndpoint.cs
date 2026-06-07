using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

public class AuthorizePaymentEndpoint : EndpointWithoutRequest<PaymentIntentResponse>
{
    private readonly IPaymentService _paymentService;

    public AuthorizePaymentEndpoint(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public override void Configure()
    {
        Post("/payments/intents/{id}/authorize");
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Authorize a payment intent";
            s.Description = "Authorizes a pending payment intent, moving it to the Authorized state so it can subsequently be captured.";
            s.Response(200, "Payment authorized successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment intent not found");
            s.Response(422, "Payment cannot be authorized (wrong state, or no resolved payer / payment method)");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var result = await _paymentService.AuthorizePaymentAsync(id, ct);

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
            // A missing intent is a 404; a wrong-state transition or an unresolved
            // payer/method (the externally-material guard) is a 422 — matching the
            // Billing endpoints' convention.
            AddError(ex.Message);
            await Send.ErrorsAsync(ex.Message.Contains("not found") ? 404 : 422, ct);
        }
    }
}
