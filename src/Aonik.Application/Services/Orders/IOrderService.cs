using Aonik.Application.Models.Orders;
using Aonik.Application.Models.Identity;

namespace Aonik.Application.Services.Orders;

public interface IOrderService
{
    Task<PagedResult<OrderListItem>> ListOrdersAsync(
        ListOrdersRequest request,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> CreateBillPaymentOrderAsync(
        CreateBillPaymentOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> GetOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderItemResponse> AddBillPaymentItemAsync(
        Guid orderId,
        CreateBillPaymentItemRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderItemResponse> UpdateBillPaymentItemAsync(
        Guid orderId,
        Guid orderItemId,
        UpdateBillPaymentItemRequest request,
        CancellationToken cancellationToken = default);

    Task RemoveBillPaymentItemAsync(
        Guid orderId,
        Guid orderItemId,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> SubmitOrderAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<BillPaymentOrderResponse> CancelOrderAsync(
        Guid orderId,
        string? reason,
        CancellationToken cancellationToken = default);
}
