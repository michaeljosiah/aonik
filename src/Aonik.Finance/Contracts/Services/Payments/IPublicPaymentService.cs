using Aonik.Finance.Contracts.Models.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

public interface IPublicPaymentService
{
    Task<GuestPaymentIntentResponse> CreateGuestPaymentIntentAsync(
        CreateGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<GuestPaymentIntentStatusResponse?> GetGuestPaymentIntentStatusAsync(
        GetGuestPaymentIntentStatusRequest request,
        CancellationToken cancellationToken = default);
}
