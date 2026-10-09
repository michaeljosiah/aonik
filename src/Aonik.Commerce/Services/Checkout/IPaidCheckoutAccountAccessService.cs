using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Commerce.Services.Checkout;

public interface IPaidCheckoutAccountAccessService
{
    Task IssueAsync(Guid orderId, Guid paymentIntentId, CancellationToken cancellationToken = default);
    Task LinkAsync(AccountAccessVerifiedEvent verified, CancellationToken cancellationToken = default);
}
