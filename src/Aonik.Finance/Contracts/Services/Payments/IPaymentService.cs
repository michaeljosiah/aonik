using Aonik.Finance.Contracts.Models.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

public interface IPaymentService
{
    Task<PaymentIntentResponse> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentIntentResponse?> GetPaymentIntentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);
    Task<PaymentIntentResponse> CapturePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);
    Task<PaymentIntentResponse> CancelPaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);
}
