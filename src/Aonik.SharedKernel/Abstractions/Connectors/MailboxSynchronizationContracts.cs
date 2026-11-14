namespace Aonik.SharedKernel.Abstractions.Connectors;

public sealed record MailboxProviderCall(Guid ConnectionId, string ProviderAccountId, string AccessToken)
{
    public override string ToString() => $"MailboxProviderCall({ConnectionId:D}, credentials redacted)";
}
public sealed record MailboxProfile(string EmailAddress, string HistoryId);
public sealed record MailboxWatch(string HistoryId, DateTime ExpiresAtUtc);
public sealed record MailboxChange(string HistoryId, string MessageId, string ThreadId);
public sealed record MailboxHistoryPage(string HistoryId, IReadOnlyList<MailboxChange> AddedMessages, string? NextPageToken);
public sealed record MailboxMessageReference(string Id, string ThreadId, DateTime ReceivedAtUtc, IReadOnlyList<string> LabelIds);
public sealed record MailboxMessagePage(IReadOnlyList<MailboxMessageReference> Messages, string? NextPageToken);

/// <summary>Fixed provider endpoints used by the authorized subscription service; no browser or model receives credentials.</summary>
public interface IConnectorMailboxSyncProvider
{
    string ConnectorKey { get; }
    bool IsConfigured { get; }
    string ApplicationKey { get; }
    string? PushTopic { get; }
    string? PushSubscription { get; }
    Task<MailboxProfile> GetProfileAsync(MailboxProviderCall call, CancellationToken cancellationToken = default);
    Task<MailboxWatch> WatchAsync(MailboxProviderCall call, string topic, IReadOnlyList<string> labelIds, CancellationToken cancellationToken = default);
    Task StopWatchAsync(MailboxProviderCall call, CancellationToken cancellationToken = default);
    Task<MailboxHistoryPage> ReadHistoryAsync(MailboxProviderCall call, string startHistoryId, string? pageToken, CancellationToken cancellationToken = default);
    Task<MailboxMessagePage> ListMessagesAsync(MailboxProviderCall call, IReadOnlyList<string> labelIds, DateTime? sinceUtc, string? pageToken, CancellationToken cancellationToken = default);
    Task<MailboxMessageReference> GetMessageAsync(MailboxProviderCall call, string messageId, CancellationToken cancellationToken = default);
}

public sealed record ConnectorSubscriptionBinding(Guid BindingId, int Revision, Guid WorkflowId, Guid WorkflowVersionId,
    Guid AutomationIdentityId, WorkflowConnectorBinding Connection, string Mode, int PollIntervalMinutes,
    IReadOnlyList<string> LabelIds, bool AllowPollingFallback, DateTime? BackfillSinceUtc, DateTime? ActivatedAtUtc = null,
    string SubjectType = "Invoice", string? SubjectPrefix = null, bool IgnoreSelfGenerated = true);

public interface IWorkflowMailboxBindingReader
{
    Task<ConnectorSubscriptionBinding> RequireActiveAsync(Guid bindingId, int revision, CancellationToken cancellationToken = default);
    Task<bool> IsDisabledAsync(Guid bindingId, CancellationToken cancellationToken = default);
}

public interface IConnectorSubscriptionService
{
    Task ValidateAsync(ConnectorSubscriptionBinding binding, CancellationToken cancellationToken = default);
    Task<Guid> ActivateAsync(Guid bindingId, int revision, CancellationToken cancellationToken = default);
    Task DisableBindingAsync(Guid bindingId, int revision, CancellationToken cancellationToken = default);
    Task SynchronizeAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
}

public sealed record VerifiedMailboxPush(string ApplicationKey, string Subscription, string EmailAddress, string HistoryId, string ProviderMessageId);
public interface IConnectorMailboxPushVerifier
{
    Task<VerifiedMailboxPush> VerifyAsync(string authorization, string envelopeJson, CancellationToken cancellationToken = default);
}
public interface IConnectorMailboxPushIngress
{
    Task AcceptAsync(string authorization, string envelopeJson, CancellationToken cancellationToken = default);
}
