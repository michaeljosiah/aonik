using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.GiftCards;

/// <summary>Immutable attribution of an instrument to one actual canonical liability leg.</summary>
public sealed class GiftCardOperation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid GiftCardId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public Guid? OriginalOperationId { get; set; }
    public Guid OrderId { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid JournalEntryLineId { get; set; }
    public Guid? PaymentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}
