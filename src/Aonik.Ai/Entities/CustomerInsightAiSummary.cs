using Aonik.SharedKernel.Primitives;

namespace Aonik.Ai.Entities;

public class CustomerInsightAiSummary : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid CustomerInsightSnapshotId { get; set; }
    public Guid AiRunId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime AsOfUtc { get; set; }
    public string NarrativeVersion { get; set; } = string.Empty;
    public string SummaryJson { get; set; } = string.Empty;
    public Guid? SupersededById { get; set; }
    public string? FailureReason { get; set; }
}
