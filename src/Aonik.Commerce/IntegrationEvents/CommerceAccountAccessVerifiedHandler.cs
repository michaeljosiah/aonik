using Aonik.Commerce.Services.Checkout;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Abstractions.Loyalty;

namespace Aonik.Commerce.IntegrationEvents;

internal sealed class CommerceAccountAccessVerifiedHandler(IPaidCheckoutAccountAccessService accountAccess,
    ILoyaltyService? loyalty = null)
    : IEventHandler<AccountAccessVerifiedEvent>
{
    public async Task HandleAsync(AccountAccessVerifiedEvent @event, CancellationToken cancellationToken = default)
    {
        await accountAccess.LinkAsync(@event, cancellationToken);
        // Linking may already have committed on a previous delivery. Keep the ledger transfer
        // retryable, after the existing paid-source and verified-identity checks.
        if (loyalty is not null)
            await loyalty.AttachVerifiedGuestAsync(@event, cancellationToken);
    }
}
