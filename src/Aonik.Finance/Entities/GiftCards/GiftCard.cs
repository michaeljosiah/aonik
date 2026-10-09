using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.GiftCards;

/// <summary>Bearer instrument and original issuance metadata; value lives in its journal legs.</summary>
public sealed class GiftCard : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CartId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public Guid OrderItemId { get; set; }
    public int ItemIndex { get; set; }
    public decimal FaceValue { get; set; }
    public string Currency { get; set; } = "GBP";
    public string PolicySnapshotJson { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public string ProtectedCode { get; set; } = string.Empty;
    public string MaskedCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
