using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.GiftCards;

/// <summary>Frozen tender or issuance instruction; reservations follow the payment's proven outcome.</summary>
public sealed class GiftCardCheckoutAttempt : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CartId { get; set; }
    public Guid? GiftCardId { get; set; }
    public decimal ReservedAmount { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Released";
}
