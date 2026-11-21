using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Integrations;

public sealed class ConnectorSubscription : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid ConnectionId { get; set; }
    public string ConnectorKey { get; set; } = string.Empty;
    public string ApplicationKey { get; set; } = string.Empty;
    public string ProviderAccountHash { get; set; } = string.Empty;
    public string? WatchClaim { get; set; }
    public string Status { get; set; } = "Active";
    public string Mode { get; set; } = "Polling";
    public string BindingsJson { get; set; } = "[]";
    public string? HistoryId { get; set; }
    public string? TargetHistoryId { get; set; }
    public string? PageToken { get; set; }
    public bool FullSync { get; set; }
    public DateTime? FullSyncStartedAtUtc { get; set; }
    public DateTime? FullSyncSinceUtc { get; set; }
    public int FullSyncPages { get; set; }
    public DateTime? WatchExpiresAtUtc { get; set; }
    public DateTime? WatchRenewedAtUtc { get; set; }
    public DateTime? NextPollAtUtc { get; set; }
    public DateTime? LastSuccessAtUtc { get; set; }
    public string? LastError { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public Guid PollTaskId { get; set; }
    public string PollTaskProof { get; set; } = string.Empty;
    public DateTime PollTaskStartAtUtc { get; set; }
    public DateTime PollTaskEndAtUtc { get; set; }
}
