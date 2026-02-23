using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;

namespace Aonik.Finance.Endpoints.Payments;

public class CancelPaymentEndpoint : EndpointWithoutRequest<PaymentIntentResponse>
{
    private readonly IPaymentService _paymentService;

    public CancelPaymentEndpoint(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    public override void Configure()
    {
        Post("/payments/intents/{id}/cancel");
        Policies("AdminUserPolicy");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        try
        {
            var result = await _paymentService.CancelPaymentAsync(id, ct);

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
