using System.Numerics;

using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorMailboxHintHandler(PlatformDbContext database, ITenantProvider tenantProvider, ConnectorSubscriptionService subscriptions, IClock clock)
    : IEventHandler<ConnectorMailboxHintEvent>
{
    public async Task HandleAsync(ConnectorMailboxHintEvent message, CancellationToken cancellationToken = default)
    {
        if (message.TenantId != tenantProvider.GetCurrentTenantId() || message.EventId == Guid.Empty || !BigInteger.TryParse(message.HistoryId, out BigInteger hinted) || hinted <= 0)
            throw new ArgumentException("Invalid mailbox hint scope.");
        var subscription = await database.ConnectorSubscriptions.SingleOrDefaultAsync(value => value.TenantId == message.TenantId && value.Id == message.SubscriptionId
            && value.Status != "Disabled" && value.Mode == "PushWithPolling" && value.WatchClaim != null, cancellationToken);
        if (subscription is null || (BigInteger.TryParse(subscription.HistoryId, out BigInteger current) && hinted <= current)) return;
        subscription.NextPollAtUtc = clock.UtcNow;
        await database.SaveChangesAsync(cancellationToken);
        await subscriptions.SynchronizeAsync(subscription.Id, cancellationToken);
    }
}
