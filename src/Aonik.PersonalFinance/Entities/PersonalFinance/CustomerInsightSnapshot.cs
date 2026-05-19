using Aonik.SharedKernel.Primitives;

namespace Aonik.Finance.Entities.PersonalFinance;

public class CustomerInsightSnapshot : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime AsOfUtc { get; set; }
    public DateTime WindowStartUtc { get; set; }
    public DateTime WindowEndUtc { get; set; }
    public int Version { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = string.Empty;
    public int? GenerationDurationMs { get; set; }
    public string? FailureReason { get; set; }
    public Guid? SupersededById { get; set; }
}
