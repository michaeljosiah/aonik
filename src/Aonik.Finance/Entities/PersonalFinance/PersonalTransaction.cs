using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class PersonalTransaction : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid? PersonalAccountId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public Guid SourceId { get; set; }
    public DateTime OccurredAt { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? Merchant { get; set; }
    public string? Category { get; set; }
    public decimal Confidence { get; set; }
    public string? CategorisedBy { get; set; }
    public string? Notes { get; set; }
    public string TagsJson { get; set; } = string.Empty;
}
