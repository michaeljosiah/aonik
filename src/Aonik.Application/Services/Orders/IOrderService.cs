using Aonik.Application.Models.Orders;

namespace Aonik.Application.Services.Orders;

public interface IOrderService
{
    Task<ValidateDuplicateOrderResponse> ValidateDuplicateAsync(
        ValidateDuplicateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateOrderResponse> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<OrderDetailResponse?> GetAsync(
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task<OrderListResponse> ListAsync(
        OrderListQuery query,
        CancellationToken cancellationToken = default);
}
