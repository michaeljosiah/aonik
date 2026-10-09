using Aonik.SharedKernel.Primitives;

namespace Aonik.Commerce.Entities.Fulfilment;

/// <summary>Immutable purchase source and delivery facts, with operational submission/dispatch audit.</summary>
public class OrderGiftCardDelivery : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid CartId { get; set; }
    public Guid OrderId { get; set; }
    public Guid PaymentIntentId { get; set; }
    public Guid OrderItemId { get; set; }
    public int ItemIndex { get; set; }
    public string PurchaseSnapshotJson { get; set; } = string.Empty;
    public string DeliveryMethod { get; set; } = string.Empty;
    public DateTime? SendAtUtc { get; set; }
    public DateOnly? PostingDate { get; set; }
    public Guid? GiftCardId { get; set; }
    public string MaskedCode { get; set; } = string.Empty;
    public DateTime? ExpiresAtUtc { get; set; }
    public string Status { get; set; } = "PendingIssuance";
    public int SendSequence { get; set; }
    public int SentSequence { get; set; }
    public DateTime? SequenceDueAtUtc { get; set; }
    public DateTime? LastSentAtUtc { get; set; }
    public DateTime? LastResendRequestedAtUtc { get; set; }
    public DateTime? ResendWindowStartedAtUtc { get; set; }
    public int ResendsInWindow { get; set; }
    public DateTime? LastPrintedAtUtc { get; set; }
    public Guid? LastPrintedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? CompletedBy { get; set; }
}
