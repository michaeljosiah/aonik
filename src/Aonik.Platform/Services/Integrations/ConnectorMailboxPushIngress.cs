using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Aonik.Platform.Entities.Identity;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Events.Integration;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Platform.Services.Integrations;

internal sealed class ConnectorMailboxPushIngress(IConnectorMailboxPushVerifier verifier, PlatformDbContext lookup, IServiceScopeFactory scopes, IClock clock)
    : IConnectorMailboxPushIngress
{
    public async Task AcceptAsync(string authorization, string envelopeJson, CancellationToken cancellationToken = default)
    {
        VerifiedMailboxPush verified = await verifier.VerifyAsync(authorization, envelopeJson, cancellationToken);
        string accountHash = ConnectorSubscriptionService.AccountHash(verified.EmailAddress);
        string claim = ConnectorSubscriptionService.Hash(verified.ApplicationKey + ":" + accountHash);
        // Only signature/audience/issuer/service-account-verified ingress can make this lookup.
        var target = await lookup.ConnectorSubscriptions.AcrossTenants().AsNoTracking().Where(value => value.ApplicationKey == verified.ApplicationKey
            && value.ProviderAccountHash == accountHash && value.WatchClaim == claim && value.Mode == "PushWithPolling" && value.Status != "Disabled")
            .Select(value => new { value.Id, value.TenantId }).SingleOrDefaultAsync(cancellationToken);
        if (target is null) throw new ConnectorAccessException("MailboxPushUnauthorized", "The notification does not identify an active local watch.");
        using IServiceScope scope = scopes.CreateScope();
        ITenantContext tenant = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenant.TenantId = target.TenantId; tenant.ResolutionSource = "verified-mailbox-watch";
        PlatformDbContext database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        if (!await database.Tenants.AsNoTracking().AnyAsync(value => value.Id == target.TenantId && value.Status == TenantStatus.Active, cancellationToken))
            throw new ConnectorAccessException("MailboxPushUnauthorized", "The watch tenant is unavailable.");
        Guid eventId = ConnectorSubscriptionService.EventId($"gmail-hint:{verified.ApplicationKey}:{verified.Subscription}:{verified.ProviderMessageId}");
        database.EnqueueIntegrationEvent(new ConnectorMailboxHintEvent(eventId, clock.UtcNow, target.TenantId, target.Id, verified.HistoryId));
        await database.SaveChangesAsync(cancellationToken);
    }
}
