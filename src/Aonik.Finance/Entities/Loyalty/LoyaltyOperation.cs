using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.Loyalty;

/// <summary>Immutable business provenance for one owner's actual posted liability leg.</summary>
public sealed class LoyaltyOperation : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public Guid? OrderId { get; set; }
    public Guid? OriginalOperationId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? JournalEntryLineId { get; set; }
    public long Points { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? Reason { get; set; }
    public string DetailsJson { get; set; } = "{}";
}
