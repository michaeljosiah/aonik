using Aonik.SharedKernel.Primitives;

namespace Aonik.Platform.Entities.Compliance;

public class DocumentVersion : AuditableEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? DecisionedAt { get; set; }
    public string? DecisionReason { get; set; }
    public Document? Document { get; set; }
}
