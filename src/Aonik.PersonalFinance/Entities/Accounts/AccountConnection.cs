using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities.Accounts;

public class AccountConnection : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderConnectionReference { get; set; } = string.Empty;
    public string InstitutionName { get; set; } = string.Empty;
    public string? InstitutionReference { get; set; }
    public bool AutoSyncEnabled { get; set; }
    public int SyncIntervalMinutes { get; set; }
    public DateTime? NextScheduledSyncAt { get; set; }
    public DateTime? LastWebhookReceivedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ConsentStatus { get; set; } = string.Empty;
    public string SecretReference { get; set; } = string.Empty;
    public string? SyncCursor { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public string? LastError { get; set; }
    public DateTime? DisconnectedAt { get; set; }
}
