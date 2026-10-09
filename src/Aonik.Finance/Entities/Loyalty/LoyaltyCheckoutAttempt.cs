using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Loyalty;

/// <summary>Frozen reward instruction and its reservation; owned by the payment's transaction.</summary>
public sealed class LoyaltyCheckoutAttempt : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid CartId { get; set; }
    public Guid? AccountId { get; set; }
    public long ReservedPoints { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public string Status { get; set; } = "Reserved";
}
