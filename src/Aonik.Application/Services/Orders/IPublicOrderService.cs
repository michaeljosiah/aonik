using Aonik.Application.Models.Orders;

namespace Aonik.Application.Services.Orders;

public interface IPublicOrderService
{
    Task<GuestBillPaymentDraftResponse> CreateGuestBillPaymentDraftAsync(
        CreateGuestBillPaymentDraftRequest request,
        CancellationToken cancellationToken = default);

    Task<GuestBillPaymentDraftDetailResponse?> GetGuestBillPaymentDraftAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);
}
