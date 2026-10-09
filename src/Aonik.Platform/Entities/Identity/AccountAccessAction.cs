using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Identity;

public sealed class AccountAccessAction : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public Guid? CartId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? PaymentIntentId { get; set; }
    public Guid? GuestPartyId { get; set; }
    public Guid? UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? OriginalEmail { get; set; }
    public string? ExternalIssuer { get; set; }
    public string? ExternalSubject { get; set; }
    public long? IdentityRevision { get; set; }
    public Guid Generation { get; set; }
    public DateTime? DeliveryStartedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime? ConsumedAtUtc { get; set; }
    public Guid? ConsumedByUserId { get; set; }
    public DateTime SendWindowStartedAtUtc { get; set; }
    public int SendCount { get; set; }
}
