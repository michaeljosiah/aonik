using Aonik.Finance.Contracts.Models.Payments;

namespace Aonik.Finance.Contracts.Services.Payments;

public interface IPublicPaymentService
{
    Task<GuestPaymentIntentResponse> CreateGuestPaymentIntentAsync(
        CreateGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Spec 042 — creates a guest payment intent for a commerce <c>ProductPurchase</c> order, funding
    /// the explicit checkout total (after discount/tax). Permission-free public path.
    /// </summary>
    Task<GuestPaymentIntentResponse> CreateCommerceGuestPaymentIntentAsync(
        CreateCommerceGuestPaymentIntentRequest request,
        CancellationToken cancellationToken = default);

    Task<GuestPaymentIntentStatusResponse?> GetGuestPaymentIntentStatusAsync(
        GetGuestPaymentIntentStatusRequest request,
        CancellationToken cancellationToken = default);
}
