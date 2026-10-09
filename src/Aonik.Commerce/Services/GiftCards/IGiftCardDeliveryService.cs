using Aonik.Commerce.Contracts.Models.Catalog;
using Aonik.Commerce.Contracts.Models.GiftCards;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Commerce.Services.GiftCards;

public interface IGiftCardDeliveryService
{
    Task ActivateAsync(GiftCardIssuedEvent issued, CancellationToken cancellationToken = default);
    Task<TaskActionResult> SendAsync(TaskActionContext context, Guid deliveryId, int sequence, CancellationToken cancellationToken = default);
    Task<PagedResult<SentGiftCardDto>> ListSentAsync(Guid partyId, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task RequestResendAsync(Guid deliveryId, Guid partyId, CancellationToken cancellationToken = default);
    Task<PagedResult<PhysicalGiftCardDto>> ListPhysicalAsync(DateOnly? dueThrough = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default);
    Task<GiftCardPrintDto> PrintAsync(Guid deliveryId, CancellationToken cancellationToken = default);
    Task<PhysicalGiftCardDto> CompletePhysicalAsync(Guid deliveryId, string expectedVersion, CancellationToken cancellationToken = default);
}
