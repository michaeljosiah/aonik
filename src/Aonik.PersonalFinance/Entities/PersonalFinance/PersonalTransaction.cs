using Aonik.SharedKernel.Primitives;

namespace Aonik.PersonalFinance.Entities;

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
    public string? Description { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public decimal Confidence { get; set; }
    public string? CategorisedBy { get; set; }
    public string? ClassificationMethod { get; set; }
    public string? ClassifierVersion { get; set; }
    public Guid? AiRunId { get; set; }
    public string? ReviewStatus { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public string? ImportFingerprint { get; set; }
    public string? Notes { get; set; }
    public string TagsJson { get; set; } = string.Empty;
    public Guid? FinancialContextId { get; set; }

    public List<TransactionAttachment> Attachments { get; set; } = new();
}
