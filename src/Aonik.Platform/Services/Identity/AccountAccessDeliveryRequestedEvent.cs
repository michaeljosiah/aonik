using Aonik.Platform.Contracts.Services.Identity;
using Aonik.SharedKernel.Events;

namespace Aonik.Platform.Services.Identity;

public sealed record AccountAccessDeliveryRequestedEvent(Guid TenantId, Guid ActionId, Guid Generation) : ITenantScopedEvent;

internal sealed class AccountAccessDeliveryRequestedHandler(IAccountAccessService service)
    : IEventHandler<AccountAccessDeliveryRequestedEvent>
{
    public Task HandleAsync(AccountAccessDeliveryRequestedEvent @event, CancellationToken cancellationToken = default)
        => service.DeliverAsync(@event.ActionId, @event.Generation, cancellationToken);
}
