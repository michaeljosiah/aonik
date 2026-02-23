using Aonik.Finance.Contracts.Models.Orders;

namespace Aonik.Finance.Contracts.Services.Orders;

public interface IPublicOrderService
{
    Task<GuestBillPaymentDraftResponse> CreateGuestBillPaymentDraftAsync(
        CreateGuestBillPaymentDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<GuestBillPaymentDraftDetailResponse?> GetGuestBillPaymentDraftAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
