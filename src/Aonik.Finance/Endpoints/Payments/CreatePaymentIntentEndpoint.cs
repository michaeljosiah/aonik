using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

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
        Policies("AdminUserWritePolicy");
        Summary(s =>
        {
            s.Summary = "Create a payment intent";
            s.Description = "Creates a new payment intent for a specified amount and currency, optionally linked to an order or invoice.";
            s.Response(201, "Payment intent created successfully");
            s.Response(400, "Invalid request data");
            s.Response(401, "Not authenticated");
            s.Response(404, "Referenced order not found");
            s.Response(422, "Referenced order cannot fund a payment intent");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CreatePaymentIntentRequest req, CancellationToken ct)
    {
        var appRequest = new Finance.Contracts.Models.Payments.CreatePaymentIntentRequest(
            req.Amount,
            req.Currency,
            req.Reference,
            req.OrderId,
            req.InvoiceId,
            PaymentMethodType: req.PaymentMethodType);

        try
        {
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
        catch (InvalidOperationException ex)
        {
            // e.g. the referenced order does not exist for this tenant — a 4xx, not a 500.
            AddError(ex.Message);
            await Send.ErrorsAsync(ex.Message.Contains("not found") ? 404 : 422, ct);
        }
    }
}
