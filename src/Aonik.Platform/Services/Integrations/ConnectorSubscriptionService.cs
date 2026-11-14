using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

using Aonik.Platform.Entities.Integrations;
using Aonik.Platform.Persistence;
using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Abstractions.Connectors;
using Aonik.SharedKernel.Abstractions.Multitenancy;
using Aonik.SharedKernel.Abstractions.Tasks;
using Aonik.SharedKernel.Persistence;

namespace Aonik.Platform.Services.Integrations;

internal sealed partial class ConnectorSubscriptionService(PlatformDbContext database, ITenantProvider tenantProvider,
    IWorkflowMailboxBindingReader bindings, ConnectorAuthorization authorization, ConnectorCatalog catalog, ConnectorCredentialService credentials,
    IEnumerable<IConnectorMailboxSyncProvider> providers, IDataProtectionProvider protection, ITaskService tasks, IClock clock) : IConnectorSubscriptionService
{
    private static readonly ConnectorActionRef SyncAction = new(GmailActionKeys.Search, 1);
    private IConnectorMailboxSyncProvider Provider => providers.SingleOrDefault(provider => provider.ConnectorKey == GmailActionKeys.Connector && provider.IsConfigured)
        ?? throw ConnectorAuthorization.Access("ConnectorUnavailable");

    public async Task ValidateAsync(ConnectorSubscriptionBinding binding, CancellationToken cancellationToken = default)
    {
        if (binding.Mode is not ("Polling" or "PushWithPolling") || binding.PollIntervalMinutes is < 5 or > 1440
            || !binding.Connection.AllowedActions.Contains(SyncAction)
            || (binding.SubjectType == "Document"
                ? binding.Connection.AllowedActions.Count != 3 || GmailActionKeys.ReadActions.Any(key => !binding.Connection.AllowedActions.Contains(new(key, 1)))
                : binding.SubjectType != "Invoice" || binding.Connection.AllowedActions.Count != 1)) throw new ArgumentException("Invalid mailbox synchronization bounds.");
        if (binding.Mode == "PushWithPolling" && Provider.PushTopic is null && !binding.AllowPollingFallback)
            throw new ConnectorAccessException("MailboxPushNotConfigured", "Provision authenticated Gmail push, or explicitly approve polling fallback.");
        await RequireUseAsync(binding, cancellationToken);
        foreach (ConnectorActionRef action in binding.Connection.AllowedActions)
            await authorization.RequireUseAsync(binding.Connection.ConnectionId, catalog.Resolve(action),
                new(new("AutomationIdentity", binding.AutomationIdentityId, WorkflowVersionId: binding.WorkflowVersionId), binding.WorkflowId,
                    binding.WorkflowVersionId, PinnedGrantId: binding.Connection.WorkflowGrantId, AllowedActions: binding.Connection.AllowedActions), cancellationToken);
    }

    public async Task<Guid> ActivateAsync(Guid bindingId, int revision, CancellationToken cancellationToken = default)
    {
        Guid tenantId = tenantProvider.GetCurrentTenantId();
        ConnectorSubscriptionBinding binding = await bindings.RequireActiveAsync(bindingId, revision, cancellationToken);
        await ValidateAsync(binding, cancellationToken);
        ConnectorConnection connection = await RequireUseAsync(binding, cancellationToken);
        IConnectorMailboxSyncProvider provider = Provider;
        string token = await credentials.AccessTokenAsync(connection.Id, cancellationToken);
        MailboxProfile profile = await provider.GetProfileAsync(new(connection.Id, connection.ProviderAccountId, token), cancellationToken);
        connection = await RequireUseAsync(await bindings.RequireActiveAsync(bindingId, revision, cancellationToken), cancellationToken);
        if (!string.Equals(profile.EmailAddress, connection.ProviderAccountLabel, StringComparison.OrdinalIgnoreCase)) throw new ConnectorProviderException("ProviderAccountChanged");
        ConnectorSubscription? subscription = await database.ConnectorSubscriptions.SingleOrDefaultAsync(value => value.TenantId == tenantId
            && value.ConnectionId == connection.Id, cancellationToken);
        if (subscription?.LeaseExpiresAtUtc > clock.UtcNow) throw new ConnectorProviderException("MailboxSynchronizationBusy", true);
        if (subscription is null)
        {
            subscription = new() { TenantId = authorization.TenantId, ConnectionId = connection.Id, ConnectorKey = provider.ConnectorKey,
                ApplicationKey = provider.ApplicationKey, ProviderAccountHash = AccountHash(profile.EmailAddress), PollTaskId = Guid.NewGuid(),
                PollTaskStartAtUtc = clock.UtcNow, PollTaskEndAtUtc = clock.UtcNow.AddDays(365) };
            subscription.PollTaskProof = ProtectTask(subscription);
            database.ConnectorSubscriptions.Add(subscription);
        }
        if (subscription.ApplicationKey != provider.ApplicationKey || subscription.ProviderAccountHash != AccountHash(connection.ProviderAccountLabel))
        {
            if (subscription.Status != "Disabled" || ReadBindings(subscription).Count != 0
                || (subscription.WatchClaim is not null && (subscription.WatchExpiresAtUtc is null || subscription.WatchExpiresAtUtc > clock.UtcNow)))
                throw new ConnectorAccessException("MailboxConfigurationChanged", "Disable the previous subscription and release its watch before reviewing a new application/account.");
            subscription.ApplicationKey = provider.ApplicationKey; subscription.ProviderAccountHash = AccountHash(profile.EmailAddress);
            subscription.WatchClaim = null; subscription.WatchExpiresAtUtc = null; subscription.WatchRenewedAtUtc = null;
            subscription.HistoryId = null; subscription.TargetHistoryId = null; subscription.PageToken = null; subscription.FullSync = false;
            subscription.FullSyncSinceUtc = null; subscription.FullSyncStartedAtUtc = null; subscription.FullSyncPages = 0;
        }
        TaskResponse? priorTask = await tasks.GetAsync(subscription.PollTaskId, cancellationToken);
        if (subscription.PollTaskEndAtUtc <= clock.UtcNow || priorTask?.Status is "Cancelled" or "Completed")
        {
            subscription.PollTaskId = Guid.NewGuid(); subscription.PollTaskStartAtUtc = clock.UtcNow; subscription.PollTaskEndAtUtc = clock.UtcNow.AddDays(365);
            subscription.PollTaskProof = ProtectTask(subscription);
        }
        List<SubscriptionBindingRef> references = ReadBindings(subscription);
        references.RemoveAll(value => value.BindingId == bindingId);
        references.Add(new(bindingId, revision));
        if (references.Count > 100) throw new ArgumentException("One mailbox supports at most one hundred approved trigger bindings.");
        List<ConnectorSubscriptionBinding> active = await ResolveAsync(references, cancellationToken);
        if (active.Count == 0) throw new PermissionDeniedException("Integration.Use");
        bool wantsPush = active.Any(value => value.Mode == "PushWithPolling");
        subscription.Mode = wantsPush && provider.PushTopic is not null ? "PushWithPolling" : "Polling";
        if (subscription.Mode == "PushWithPolling")
        {
            string claim = Hash(provider.ApplicationKey + ":" + subscription.ProviderAccountHash);
            // Read only a global uniqueness boolean; provider watches must not overwrite another tenant's watch.
            bool conflict = await database.ConnectorSubscriptions.AcrossTenants().AsNoTracking().AnyAsync(value => value.Id != subscription.Id && value.WatchClaim == claim, cancellationToken);
            if (conflict)
            {
                if (active.Any(value => value.Mode == "PushWithPolling" && !value.AllowPollingFallback))
                    throw new ConnectorAccessException("MailboxWatchConflict", "This provider account/application already has another watch. Approve polling or use isolated provider configuration.");
                subscription.Mode = "Polling"; subscription.LastError = "MailboxWatchConflict";
            }
            else subscription.WatchClaim = claim;
        }
        subscription.BindingsJson = ConnectorJson.Serialize(references.OrderBy(value => value.BindingId).ToList());
        if (subscription.Status == "NeedsAttention") subscription.FullSyncPages = 0;
        subscription.Status = "Active"; subscription.NextPollAtUtc = clock.UtcNow;
        try { await database.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException) { throw new ConnectorProviderException("MailboxConfigurationBusy", true); }
        await EnsurePollTaskAsync(subscription, cancellationToken);
        return subscription.Id;
    }

    public async Task DisableBindingAsync(Guid bindingId, int revision, CancellationToken cancellationToken = default)
    {
        // A stale disable event may remove only its old subscription membership, never a newer activation.
        if (!await bindings.IsDisabledAsync(bindingId, cancellationToken))
        {
            try { if ((await bindings.RequireActiveAsync(bindingId, revision + 1, cancellationToken)).Revision > revision) return; }
            catch (PermissionDeniedException) { return; }
        }
        List<ConnectorSubscription> subscriptions = await database.ConnectorSubscriptions.Where(value => value.TenantId == authorization.TenantId
            && value.Status != "Disabled").Take(1001).ToListAsync(cancellationToken);
        if (subscriptions.Count > 1000) throw new InvalidOperationException("Mailbox disable requires bounded subscription review.");
        foreach (ConnectorSubscription subscription in subscriptions)
        {
            List<SubscriptionBindingRef> references = ReadBindings(subscription);
            if (references.RemoveAll(value => value.BindingId == bindingId && value.Revision <= revision) == 0) continue;
            subscription.BindingsJson = ConnectorJson.Serialize(references);
            if (references.Count == 0) subscription.Status = "Disabled";
            await database.SaveChangesAsync(cancellationToken);
            if (references.Count == 0) await ReleaseWatchAsync(subscription, cancellationToken);
        }
    }

    private Task<ConnectorConnection> RequireUseAsync(ConnectorSubscriptionBinding binding, CancellationToken cancellationToken)
        => authorization.RequireUseAsync(binding.Connection.ConnectionId, catalog.Resolve(GmailActionKeys.Connector, SyncAction),
            new(new("AutomationIdentity", binding.AutomationIdentityId, WorkflowVersionId: binding.WorkflowVersionId), binding.WorkflowId,
                binding.WorkflowVersionId, PinnedGrantId: binding.Connection.WorkflowGrantId, AllowedActions: binding.Connection.AllowedActions), cancellationToken);
    private async Task<List<ConnectorSubscriptionBinding>> ResolveAsync(List<SubscriptionBindingRef> references, CancellationToken cancellationToken)
    {
        List<ConnectorSubscriptionBinding> active = [];
        foreach (SubscriptionBindingRef reference in references)
        {
            try
            {
                var binding = await bindings.RequireActiveAsync(reference.BindingId, reference.Revision, cancellationToken);
                await RequireUseAsync(binding, cancellationToken); active.Add(binding);
            }
            catch (Exception exception) when (exception is PermissionDeniedException or ConnectorAccessException) { }
        }
        return active;
    }
    private Task EnsurePollTaskAsync(ConnectorSubscription subscription, CancellationToken cancellationToken) => tasks.ScheduleAsync(new(
        "Synchronize authorized Gmail mailbox", TaskKinds.ScheduledAction, "connector_mailbox_poll", subscription.PollTaskProof, TaskAssigneeTypes.System,
        SubjectType: "ConnectorSubscription", SubjectId: subscription.Id, RecurrenceCron: "0 0/5 * * * ?", Timezone: "UTC",
        StartAtUtc: DateTime.SpecifyKind(subscription.PollTaskStartAtUtc, DateTimeKind.Utc),
        EndAtUtc: DateTime.SpecifyKind(subscription.PollTaskEndAtUtc, DateTimeKind.Utc), MaxRuns: 105120,
        SourceModule: "Platform", TaskId: subscription.PollTaskId), cancellationToken);
    private string ProtectTask(ConnectorSubscription subscription) => ConnectorJson.Serialize(new SubscriptionTaskProof(
        TaskProtector(subscription.TenantId, subscription.PollTaskId).Protect(subscription.Id.ToString("D"))));
    internal Guid ReadTask(string payload, Guid tenantId, Guid taskId)
    {
        try
        {
            if (payload.Length > 16_384) throw new ArgumentException("Invalid mailbox task proof.");
            return Guid.Parse(TaskProtector(tenantId, taskId).Unprotect(ConnectorJson.Read<SubscriptionTaskProof>(payload).Proof));
        }
        catch (Exception exception) when (exception is CryptographicException or System.Text.Json.JsonException or FormatException)
        { throw new ArgumentException("Invalid mailbox task identity or server-issued proof."); }
    }
    private IDataProtector TaskProtector(Guid tenantId, Guid taskId) => protection.CreateProtector("Aonik.MailboxSubscriptionTask.v1", tenantId.ToString("N"), taskId.ToString("N"));
    private static List<SubscriptionBindingRef> ReadBindings(ConnectorSubscription subscription) => ConnectorJson.Read<List<SubscriptionBindingRef>>(subscription.BindingsJson);
    internal static string AccountHash(string email) => Hash(email.Trim().ToLowerInvariant());
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static Guid EventId(string value) => Guid.ParseExact(Hash(value)[..32], "N");
    private sealed record SubscriptionBindingRef(Guid BindingId, int Revision);
    private sealed record SubscriptionTaskProof(string Proof);
}
