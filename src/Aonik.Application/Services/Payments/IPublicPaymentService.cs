using Aonik.Application.Models.Payments;

namespace Aonik.Application.Services.Payments;

public interface IPublicPaymentService
{
    Task<GuestPaymentIntentResponse> CreateGuestPaymentIntentAsync(
        CreateGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default);
}
