using Aonik.Api.Contracts.Payments;
using Aonik.Application.Services.Payments;
using FastEndpoints;

namespace Aonik.Api.Endpoints.Payments;

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
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        
        try
        {
            var result = await _paymentService.CapturePaymentAsync(id, ct);

            var response = new PaymentIntentResponse(
                result.Id,
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
