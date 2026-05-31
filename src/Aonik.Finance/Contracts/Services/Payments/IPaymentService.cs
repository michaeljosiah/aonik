using Aonik.Finance.Contracts.Models.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

public interface IPaymentService
{
    Task<PaymentIntentResponse> CreatePaymentIntentAsync(CreatePaymentIntentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentIntentResponse?> GetPaymentIntentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transitions a pending payment intent to <c>Authorized</c>. This is the
    /// required first step of the two-step authorize-then-capture flow; without
    /// it a freshly created (pending) intent can never reach <c>Captured</c>.
    /// </summary>
    Task<PaymentIntentResponse> AuthorizePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);
    Task<PaymentIntentResponse> CapturePaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);
    Task<PaymentIntentResponse> CancelPaymentAsync(Guid paymentIntentId, CancellationToken cancellationToken = default);
}
