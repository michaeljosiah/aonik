using Aonik.Commerce.Services.GiftCards;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Commerce.IntegrationEvents;

internal sealed class GiftCardIssuedHandler(IGiftCardDeliveryService deliveries) : IEventHandler<GiftCardIssuedEvent>
{
    public Task HandleAsync(GiftCardIssuedEvent @event, CancellationToken cancellationToken = default)
        => deliveries.ActivateAsync(@event, cancellationToken);
}
