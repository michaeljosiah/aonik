using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Commerce.IntegrationEvents;

internal sealed class CommerceAccountAccessVerifiedHandler(IPaidCheckoutAccountAccessService accountAccess)
    : IEventHandler<AccountAccessVerifiedEvent>
{
    public Task HandleAsync(AccountAccessVerifiedEvent @event, CancellationToken cancellationToken = default)
        => accountAccess.LinkAsync(@event, cancellationToken);
}
