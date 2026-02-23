using Aonik.Finance.Contracts.Api.Payments;
using Aonik.Finance.Contracts.Services.Payments;
using FastEndpoints;

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
