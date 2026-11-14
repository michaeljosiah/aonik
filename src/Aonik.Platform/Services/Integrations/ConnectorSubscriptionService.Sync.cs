using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Events.Integration;

namespace Aonik.Platform.Services.Integrations;

internal sealed partial class ConnectorSubscriptionService
{
    public async Task SynchronizeAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        ConnectorSubscription subscription = await database.ConnectorSubscriptions.SingleOrDefaultAsync(value => value.TenantId == authorization.TenantId
            && value.Id == subscriptionId, cancellationToken) ?? throw new KeyNotFoundException("Mailbox subscription not found.");
        await database.Entry(subscription).ReloadAsync(cancellationToken);
        if (subscription.Status == "Disabled") { await ReleaseWatchAsync(subscription, cancellationToken); return; }
        if (subscription.Status == "NeedsAttention") return;
        if (subscription.LeaseExpiresAtUtc > clock.UtcNow) throw new ConnectorProviderException("MailboxSynchronizationBusy", true);
        if (subscription.NextPollAtUtc > clock.UtcNow && subscription.PageToken is null) return;
        Guid leaseId = Guid.NewGuid();
        subscription.LeaseId = leaseId; subscription.LeaseExpiresAtUtc = clock.UtcNow.AddMinutes(2);
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConnectorProviderException("MailboxSynchronizationBusy", true); }
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        bounded.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            List<ConnectorSubscriptionBinding> active = await ResolveAsync(ReadBindings(subscription), bounded.Token);
            if (active.Count == 0) throw new ConnectorAccessException("MailboxAuthorityChanged", "No currently authorized trigger can read this mailbox.");
            ConnectorSubscriptionBinding selected = active[0];
            ConnectorConnection connection = await RequireUseAsync(selected, bounded.Token);
            IConnectorMailboxSyncProvider provider = Provider;
            if (subscription.ApplicationKey != provider.ApplicationKey || subscription.ProviderAccountHash != AccountHash(connection.ProviderAccountLabel))
                throw new ConnectorAccessException("MailboxConfigurationChanged", "The approved application or account changed.");
            string token = await credentials.AccessTokenAsync(connection.Id, bounded.Token);
            var call = new MailboxProviderCall(connection.Id, connection.ProviderAccountId, token);
            string? watchError = null;
            if (subscription.Mode == "Polling" && subscription.WatchClaim is not null) await ReleaseWatchAsync(subscription, bounded.Token, leaseId);
            if (subscription.Mode == "PushWithPolling" && (subscription.WatchExpiresAtUtc <= clock.UtcNow.AddDays(1)
                || subscription.WatchRenewedAtUtc is null || subscription.WatchRenewedAtUtc <= clock.UtcNow.AddDays(-1)))
            {
                if (subscription.WatchClaim is null || provider.PushTopic is null) throw new ConnectorAccessException("MailboxPushNotConfigured", "The approved watch configuration is unavailable.");
                _ = await RequireCurrentUseAsync(selected, bounded.Token);
                try
                {
                    MailboxWatch watch = await provider.WatchAsync(call, provider.PushTopic, [], bounded.Token);
                    if (watch.ExpiresAtUtc <= clock.UtcNow || watch.ExpiresAtUtc > clock.UtcNow.AddDays(8)) throw new ConnectorProviderException("MailboxWatchExpiryInvalid");
                    await FenceAsync(subscription, leaseId, bounded.Token);
                    _ = await RequireCurrentUseAsync(selected, bounded.Token);
                    subscription.WatchExpiresAtUtc = watch.ExpiresAtUtc; subscription.WatchRenewedAtUtc = clock.UtcNow;
                    await database.SaveChangesAsync(bounded.Token);
                }
                catch (ConnectorProviderException exception) when (exception.Transient) { watchError = exception.Code; }
            }
            if (subscription.HistoryId is null && !subscription.FullSync) await BeginFullSyncAsync(subscription, provider, call, active, selected, leaseId, bounded.Token);
            int pages = 0;
            do
            {
                if (++pages > 2) break;
                _ = await RequireCurrentUseAsync(selected, bounded.Token);
                string? nextPage;
                string? pageHistory = null;
                IReadOnlyList<MailboxMessageReference> references;
                if (subscription.FullSync)
                {
                    if (subscription.FullSyncPages >= 200) throw new ConnectorProviderException("MailboxRecoveryPageLimit");
                    MailboxMessagePage page = await provider.ListMessagesAsync(call, [], subscription.FullSyncSinceUtc, subscription.PageToken, bounded.Token);
                    if (page.Messages.Count > 50) throw new ConnectorProviderException("MailboxPageLimit");
                    references = page.Messages; nextPage = page.NextPageToken;
                }
                else
                {
                    MailboxHistoryPage page;
                    try { page = await provider.ReadHistoryAsync(call, subscription.HistoryId!, subscription.PageToken, bounded.Token); }
                    catch (ConnectorProviderException exception) when (exception.Code == "MailboxCursorExpired")
                    {
                        await BeginFullSyncAsync(subscription, provider, call, active, selected, leaseId, bounded.Token);
                        pages--; continue;
                    }
                    if (page.AddedMessages.Count > 200) throw new ConnectorProviderException("MailboxPageLimit");
                    references = page.AddedMessages.DistinctBy(value => value.MessageId).Select(value => new MailboxMessageReference(value.MessageId, value.ThreadId, DateTime.UnixEpoch, [])).ToList();
                    nextPage = page.NextPageToken; pageHistory = page.HistoryId;
                }
                List<MailboxMessageReference> discovered = [];
                foreach (MailboxMessageReference reference in references.DistinctBy(value => value.Id))
                {
                    _ = await RequireCurrentUseAsync(selected, bounded.Token);
                    try { discovered.Add(await provider.GetMessageAsync(call, reference.Id, bounded.Token)); }
                    catch (ConnectorProviderException exception) when (exception.Code == "ProviderItemNotFound") { }
                }
                await FenceAsync(subscription, leaseId, bounded.Token);
                _ = await RequireCurrentUseAsync(selected, bounded.Token);
                // Progress and normalized source references share this save. Mail bodies never enter the outbox.
                foreach (MailboxMessageReference message in discovered)
                {
                    if (message.ReceivedAtUtc.Kind != DateTimeKind.Utc || message.ReceivedAtUtc > clock.UtcNow.AddMinutes(5)) throw new ConnectorProviderException("ProviderResponseInvalid");
                    if (subscription.FullSync && message.ReceivedAtUtc < subscription.FullSyncSinceUtc) continue;
                    Guid eventId = EventId($"gmail-message:{subscription.TenantId:D}:{subscription.ConnectionId:D}:{message.Id}");
                    database.EnqueueIntegrationEvent(new ConnectorMessageAddedEvent(eventId, clock.UtcNow, subscription.TenantId, subscription.ConnectionId,
                        message.Id, message.ThreadId, message.ReceivedAtUtc, message.LabelIds, SelfGenerated: message.LabelIds.Contains("SENT") || message.LabelIds.Contains("DRAFT")));
                }
                subscription.PageToken = nextPage;
                if (subscription.FullSync) subscription.FullSyncPages++;
                else subscription.TargetHistoryId ??= pageHistory;
                if (nextPage is null)
                {
                    subscription.HistoryId = subscription.TargetHistoryId ?? pageHistory ?? subscription.HistoryId;
                    subscription.TargetHistoryId = null; subscription.FullSync = false; subscription.FullSyncPages = 0;
                    subscription.FullSyncStartedAtUtc = null; subscription.FullSyncSinceUtc = null;
                }
                subscription.LastSuccessAtUtc = clock.UtcNow; subscription.LastError = watchError; subscription.Status = "Active";
                subscription.NextPollAtUtc = nextPage is null ? clock.UtcNow.AddMinutes(active.Min(value => value.PollIntervalMinutes)) : clock.UtcNow;
                await database.SaveChangesAsync(bounded.Token);
            } while (subscription.PageToken is not null || subscription.FullSync);
        }
        catch (Exception exception) when (exception is ConnectorProviderException or ConnectorAccessException or PermissionDeniedException
            or DbUpdateConcurrencyException or OperationCanceledException)
        {
            // Discard uncommitted discovered events after a failed page; never advance its cursor separately.
            database.ChangeTracker.Clear();
            ConnectorSubscription current = await database.ConnectorSubscriptions.SingleAsync(value => value.TenantId == authorization.TenantId && value.Id == subscriptionId, CancellationToken.None);
            if (current.LeaseId == leaseId)
            {
                current.LastError = exception switch { ConnectorProviderException provider => provider.Code, ConnectorAccessException access => access.Code,
                    PermissionDeniedException => "MailboxAuthorityChanged", OperationCanceledException => "MailboxSynchronizationTimeout", _ => "MailboxLeaseLost" };
                current.NextPollAtUtc = clock.UtcNow.AddMinutes(5);
                if (exception is ConnectorAccessException or PermissionDeniedException) current.Status = "Suspended";
                if (exception is ConnectorProviderException { Transient: false }) current.Status = "NeedsAttention";
                try { await database.SaveChangesAsync(CancellationToken.None); } catch (DbUpdateConcurrencyException) { }
            }
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested) throw;
            throw new ConnectorProviderException(current.LastError ?? "MailboxSynchronizationFailed", true);
        }
        finally
        {
            database.ChangeTracker.Clear();
            ConnectorSubscription? current = await database.ConnectorSubscriptions.SingleOrDefaultAsync(value => value.TenantId == authorization.TenantId && value.Id == subscriptionId, CancellationToken.None);
            if (current?.LeaseId == leaseId)
            {
                current.LeaseId = null; current.LeaseExpiresAtUtc = null;
                try { await database.SaveChangesAsync(CancellationToken.None); } catch (DbUpdateConcurrencyException) { }
            }
        }
    }

    private async Task BeginFullSyncAsync(ConnectorSubscription subscription, IConnectorMailboxSyncProvider provider, MailboxProviderCall call,
        List<ConnectorSubscriptionBinding> active, ConnectorSubscriptionBinding selected, Guid leaseId, CancellationToken cancellationToken)
    {
        MailboxProfile profile = await provider.GetProfileAsync(call, cancellationToken);
        if (AccountHash(profile.EmailAddress) != subscription.ProviderAccountHash) throw new ConnectorProviderException("ProviderAccountChanged");
        await FenceAsync(subscription, leaseId, cancellationToken);
        _ = await RequireCurrentUseAsync(selected, cancellationToken);
        subscription.FullSync = true; subscription.FullSyncStartedAtUtc = clock.UtcNow; subscription.FullSyncPages = 0; subscription.PageToken = null;
        // Cursor recovery does not authorize old mail. Only an explicit approved backfill can move this bound back.
        DateTime baseline = subscription.HistoryId is null ? active.Min(value => value.ActivatedAtUtc ?? clock.UtcNow) : clock.UtcNow;
        DateTime? backfill = active.Where(value => value.BackfillSinceUtc.HasValue).Select(value => value.BackfillSinceUtc).Min();
        subscription.FullSyncSinceUtc = backfill is { } approved && approved < baseline ? approved : baseline;
        subscription.TargetHistoryId = profile.HistoryId;
        await database.SaveChangesAsync(cancellationToken);
    }
    private async Task<ConnectorConnection> RequireCurrentUseAsync(ConnectorSubscriptionBinding selected, CancellationToken cancellationToken)
        => await RequireUseAsync(await bindings.RequireActiveAsync(selected.BindingId, selected.Revision, cancellationToken), cancellationToken);
    private async Task FenceAsync(ConnectorSubscription subscription, Guid leaseId, CancellationToken cancellationToken)
    {
        await database.Entry(subscription).ReloadAsync(cancellationToken);
        if (subscription.LeaseId != leaseId || subscription.LeaseExpiresAtUtc <= clock.UtcNow || subscription.Status == "Disabled")
            throw new ConnectorProviderException("MailboxLeaseLost", true);
    }
    private async Task ReleaseWatchAsync(ConnectorSubscription subscription, CancellationToken cancellationToken, Guid? leaseId = null)
    {
        if (subscription.LeaseExpiresAtUtc > clock.UtcNow && subscription.LeaseId != leaseId) return;
        if (subscription.WatchClaim is null) return;
        bool expired = subscription.WatchExpiresAtUtc is { } expiry && expiry <= clock.UtcNow;
        if (!expired)
        {
            try
            {
                ConnectorConnection? connection = await database.ConnectorConnections.AsNoTracking().SingleOrDefaultAsync(value => value.TenantId == authorization.TenantId
                    && value.Id == subscription.ConnectionId && value.Status == "Active", cancellationToken);
                if (connection is null) return;
                string token = await credentials.AccessTokenAsync(connection.Id, cancellationToken);
                await Provider.StopWatchAsync(new(connection.Id, connection.ProviderAccountId, token), cancellationToken);
            }
            catch (Exception exception) when (exception is ConnectorAccessException or ConnectorProviderException) { subscription.LastError = "MailboxStopPending"; await database.SaveChangesAsync(cancellationToken); return; }
        }
        subscription.WatchClaim = null; subscription.WatchExpiresAtUtc = null; subscription.WatchRenewedAtUtc = null;
        await database.SaveChangesAsync(cancellationToken);
    }
}
