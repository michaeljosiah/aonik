using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace Aonik.Finance.Endpoints.Payments;

public class GetPaymentIntentEndpoint : EndpointWithoutRequest<PaymentIntentResponse>
{
    private readonly IPaymentService _paymentService;

    public GetPaymentIntentEndpoint(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public override void Configure()
    {
        Get("/payments/intents/{id}");
        Policies("AdminUserPolicy");
        Summary(s =>
        {
            s.Summary = "Get a payment intent by ID";
            s.Description = "Retrieves the details of a single payment intent, including its current status.";
            s.Response(200, "Payment intent retrieved successfully");
            s.Response(401, "Not authenticated");
            s.Response(404, "Payment intent not found");
        });
        Options(x => x.WithTags("Payments"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _paymentService.GetPaymentIntentAsync(id, ct);

        if (result == null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

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
}
