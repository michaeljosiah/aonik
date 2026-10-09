using Aonik.Commerce.Contracts.Models.Fulfilment;

namespace Aonik.Commerce.Services.Fulfilment;

public interface IOrderFulfilmentService
{
    Task<OrderFulfilmentDto> UpdateAsync(Guid orderId, UpdateOrderFulfilmentCommand command, CancellationToken cancellationToken = default);
}
