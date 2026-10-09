using Aonik.Commerce.Contracts.Models.Checkout;
using Aonik.Commerce.Contracts.Models.Fulfilment;

namespace Aonik.Commerce.Services.Fulfilment;

public interface IDeliveryReservationService
{
    Task<CartDeliveryReservationDto> GetAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);
    Task<CartDeliveryReservationDto> ReserveAsync(Guid cartId, DateOnly date, CartAccessContext access, CancellationToken cancellationToken = default);
    Task<CartDeliveryReservationDto> ReleaseAsync(Guid cartId, CartAccessContext access, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DeliveryDateCapacityDto>> GetCapacitiesAsync(DateOnly fromDate, int days = 31, CancellationToken cancellationToken = default);
    Task<DeliveryDateCapacityDto> UpdateCapacityAsync(DateOnly date, UpdateDeliveryDateCapacityRequest request, CancellationToken cancellationToken = default);
}
